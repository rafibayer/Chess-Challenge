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
        LOWERBOUND, // alpha flag
        EXACT,
        UPPERBOUND, // beta flag
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
    int timeLimit;

    public Move Think(Board board, Timer _timer)
    {
        timer = _timer;
        Move bestMove = Move.NullMove;
        int bestMoveScore = 0;
        timeLimit = 250;

        int depth = 1;
        for (; depth < 100; depth++)
        {
            int score = Negamax(board, depth, 0, -CHECKMATE, CHECKMATE, out var foundMove);

            // exit early if time timeout
            // todo: dynamic timing based on time remaining at start of turn
            if (timer.MillisecondsElapsedThisTurn > timeLimit)
                break;

            // we only update the best move if we completed the search at that ply without timing out
            bestMove = foundMove;
            bestMoveScore = score;

            // exit early if we found a checkmake
            if (score > CHECKMATE / 2)
                break;

        }

        Console.WriteLine($"{(board.IsWhiteToMove ? "White" : "Black")} found {bestMove} ~ {bestMoveScore} at {depth}");
        Console.WriteLine($"\n==== cutoffs {cutoffs} ====");
        return bestMove;
    }

    // https://en.wikipedia.org/wiki/Negamax#Negamax_with_alpha_beta_pruning_and_transposition_tables
    int Negamax(
        Board board,
        int depth,
        int ply,
        int alpha,
        int beta,
        out Move bestMove)
    {
        int originalAlpha = alpha;
        ulong zobrist = board.ZobristKey;
        bestMove = Move.NullMove;
        int bestScore = -CHECKMATE;
        bool root = ply == 0;

        if (timer.MillisecondsElapsedThisTurn > timeLimit)
            return 0;

        // discourage draw by repitition
        if (!root && board.IsRepeatedPosition())
            return -20;  

        Move ttEntryMove = Move.NullMove;
        TTEntry ttEntry = transpositionTable[zobrist % TT_LEN];
        if (ttEntry.zobrist == zobrist)
        {
            ttEntryMove = ttEntry.move;

            if (!root && ttEntry.depth >= depth 
                && ((ttEntry.flag == Flag.LOWERBOUND && ttEntry.value <= alpha) 
                    || ttEntry.flag == Flag.EXACT 
                    || (ttEntry.flag == Flag.UPPERBOUND && ttEntry.value >= beta)))
            {
                cutoffs++;
                return ttEntry.value;
            }
        }

        // negate score for descending order
        var moves = board.GetLegalMoves().OrderBy(m => -ScoreMove(m, ttEntryMove));

        // discourage getting mated, add ply to prefer
        // getting mated later to give more chances to recover
        if (!moves.Any())
            return board.IsInCheck() ? -CHECKMATE + ply : 0;

        if (depth == 0)
            return Evaluate(board);

        foreach (var move in moves)
        {
            board.MakeMove(move);

            var eval = -Negamax(board, depth - 1, ply + 1, -beta, -alpha, out _);
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

        Flag flag = bestScore >= beta 
            ? Flag.UPPERBOUND 
            : bestScore > originalAlpha 
                ? Flag.EXACT 
                : Flag.LOWERBOUND;

        transpositionTable[zobrist % TT_LEN] = new TTEntry(zobrist, bestScore, flag, bestMove, depth);
        return bestScore;
    }

    // relative to whos turn
    public int Evaluate(Board board)
    {
        int materialValue = 0;
        int mobilityValue = board.GetLegalMoves().Length;
        int captureValue = board.GetLegalMoves(true).Length;
        
        for (int i = 1; i <= 6; i++)
            materialValue += (board.GetPieceList((PieceType)i, true).Count - board.GetPieceList((PieceType)i, false).Count) * weights[i];
        
        // multiply the material value by the sign of the player,
        // negative score but black player = positive score
        return (materialValue * Sign(board.IsWhiteToMove)) + mobilityValue + captureValue;
    }

    // https://www.chessprogramming.org/MVV-LVA
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