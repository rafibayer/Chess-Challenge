using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    /*
     * Token saving:
     * use and pass lambdas with var type instead of function signature
     *  var f = (...) => ... 
     *  
     *  implicit ctors everywhere
     *      T thing = new();
     *      
     *  repeated consts
     *      double.NegativeInfinity => double inf = double.NegativeInfinity
     *      saves after enough uses
     */
    enum Flag
    {
        LOWERBOUND,
        EXACT,
        UPPERBOUND,
    }

    struct TTEntry
    {
        public ulong zobrist;
        public int value;
        public Flag flag;
        public Move move;
        public int depth;

        public TTEntry(ulong _zobrist, int _score, Flag _flag, Move _move, int _depth)
        {
            zobrist = _zobrist;
            value = _score;
            flag = _flag;
            move = _move;
            depth = _depth;
        }
    }

    int[] weights = new []
    {
        0,      // None,   // 0
        100,    // Pawn,   // 1
        300,    // Knight, // 2
        320,    // Bishop, // 3
        500,    // Rook,   // 4
        900,    // Queen,  // 5
        20000,  // King    // 6
    };


    int CHECKMATE = 100000;

    Random rng = new();

    // zobrist % len ->  zobrist, value, flag {-1 LOWER, 0 EXACT, 1 UPPER}, Move, depth
    const ulong TT_LEN = 10_000_000;
    TTEntry[] transpositionTable = new TTEntry[TT_LEN];

    // for stats only
    int cutoffs = 0;

    Timer timer;

    public Move Think(Board board, Timer _timer)
    {
        timer = _timer;
        Move bestMove = Move.NullMove;
        double bestScore = 0;

        int depth = 0;
        for (; depth < 100; depth++)
        {
            bestScore = Negamax(board, depth, -CHECKMATE, CHECKMATE, out bestMove);

            // exit early if time timeout or checkmate found
            // todo: dynamic timing based on time remaining at start of turn
            if (timer.MillisecondsElapsedThisTurn > 500 || bestScore > CHECKMATE / 2)
                break;
        }

        Console.WriteLine($"{(board.IsWhiteToMove ? "White" : "Black")} found {bestMove} ~ {bestScore} at {depth}");
        Console.WriteLine($"\n==== cutoffs {cutoffs} ====");
        return bestMove;
    }

    // https://en.wikipedia.org/wiki/Negamax#Negamax_with_alpha_beta_pruning_and_transposition_tables
    int Negamax(
        Board board,
        int depth,
        int alpha,
        int beta,
        out Move bestMove)
    {
        int originalAlpha = alpha;
        ulong zobrist = board.ZobristKey;
        bestMove = Move.NullMove;

        if (timer.MillisecondsElapsedThisTurn > 500)
            return 0;

        TTEntry ttEntry = transpositionTable[zobrist % TT_LEN];
        Move ttEntryMove = Move.NullMove;
        if (ttEntry.zobrist == zobrist)
        {
            if (ttEntry.depth >= depth)
            {
                if (ttEntry.flag == Flag.EXACT)
                {
                    bestMove = ttEntry.move;
                    return ttEntry.value;
                }
                else if (ttEntry.flag == Flag.LOWERBOUND)
                {
                    alpha = Math.Max(alpha, ttEntry.value);
                }
                else if (ttEntry.flag == Flag.UPPERBOUND)
                {
                    beta = Math.Min(beta, ttEntry.value);
                }

                if (alpha >= beta)
                {
                    cutoffs++;
                    bestMove = ttEntry.move;
                    return ttEntry.value;
                }

                ttEntryMove = ttEntry.move;
            }
        }

        var moves = board.GetLegalMoves().OrderBy(m => ScoreMove(m, ttEntryMove));
        int bestScore = -CHECKMATE;

        if (depth == 0 || !moves.Any())
        {
            return Evaluate(board, moves);
        }

        foreach (var move in moves)
        {
            board.MakeMove(move);

            var eval = -Negamax(board, depth - 1, -beta, -alpha, out _);
            board.UndoMove(move);

            if (eval >= bestScore)
            {
                bestMove = move;
                bestScore = eval;
            }

            alpha = Math.Max(alpha, eval);
            if (alpha >= beta)
            {
                cutoffs++;
                break;
            }
        }

        Flag ttFlag;
        if (bestScore <= originalAlpha)
        {
            ttFlag = Flag.UPPERBOUND;
        }
        else if (bestScore >= beta)
        {
            ttFlag = Flag.LOWERBOUND;
        }
        else
        {
            ttFlag = Flag.EXACT;
        }

        transpositionTable[zobrist % TT_LEN] = new TTEntry(zobrist, bestScore, ttFlag, bestMove, depth);
        return bestScore;
    }

    // relative to whos turn
    public int Evaluate(Board board, IEnumerable<Move> legalMoves)
    {
        if (board.IsInCheckmate())
            return -CHECKMATE;

        if (board.IsDraw())
            return -10;

        int score = 0;

        var pieces = board.GetAllPieceLists();

        // piece types
        for (int i = 0; i < 6; i++)
        {
            score += weights[i+1] * (pieces[i].Count - pieces[i + 6].Count) * Sign(board.IsWhiteToMove);
        }
       
        return score;
    }

    int ScoreMove(Move move, Move ttEntryMove)
    {
        if (move == ttEntryMove)
            return 100;

        // better to capture valuable pieces with less valuable pieces.
        // todo: we don't need piece weight here? I guess higher piece index is always better weight,
        // but it's not proportional at all
        if (move.IsCapture)
            return 10 * ((int)move.CapturePieceType) - (int)move.MovePieceType;

        return 0;
    }


    int Sign(bool white) => white ? 1 : -1;
}