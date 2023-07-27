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

    int[] weights = new int[]
    {
        0,   // None,   // 0
        100, // Pawn,   // 1
        300, // Knight, // 2
        320, // Bishop, // 3
        500, // Rook,   // 4
        900, // Queen,  // 5
        0,   // King    // 6
    };

    Random rng = new();
    Timer turnTimer;

    enum Flag
    {
        LOWERBOUND,
        EXACT,
        UPPERBOUND,
    }

    record class TTEntry
    {
        public double value;
        public Flag flag;
        public Move move;
        public int depth;

        public TTEntry(double score, Flag flag, Move move, int depthToSearch)
        {
            this.value = score;
            this.flag = flag;
            this.move = move;
            this.depth = depthToSearch;
        }
    }

    // zobrist, depth -> value, flag {-1 LOWER, 0 EXACT, 1 UPPER}, Move, depth
    Dictionary<ulong, TTEntry> transpositionTable = new();

    Dictionary<ulong, (Move, double)> tree = new();

    int cutoffs = 0;

    public Move Think(Board board, Timer timer)
    {
        turnTimer = timer;

        Move bestMove;
        double bestScore;

        int ply = 1;
        do
        {
            (bestScore, bestMove) = Negamax(board, ply, double.NegativeInfinity, double.PositiveInfinity);
            ply++;
        } 
        while (turnTimer.MillisecondsElapsedThisTurn < 500);

        Console.WriteLine($"{(board.IsWhiteToMove ? "White" : "Black")} found {bestMove} ~ {bestScore} on ply {ply}");
        Console.WriteLine($"\n==== cutoffs {cutoffs} ====");
        //DbgTree(board);
        return bestMove;
    }

    // https://en.wikipedia.org/wiki/Negamax#Negamax_with_alpha_beta_pruning_and_transposition_tables
    (double, Move) Negamax(
        Board board,
        int depth,
        double alpha,
        double beta)
    {
        double originalAlpha = alpha;
        ulong zobrist = board.ZobristKey;
        Move? ttBestMove = null;

        if (transpositionTable.TryGetValue(zobrist, out var ttEntry))
        {
            if (ttEntry.depth >= depth)
            {
                if (ttEntry.flag == Flag.EXACT)
                {
                    return (ttEntry.value, ttEntry.move);
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
                    return (ttEntry.value, ttEntry.move);
                }

                ttBestMove = ttEntry.move;
            }
        }

        // this probably has bad perf, what we're trying to do here is
        // look at the TT move first by prepending it, but also not looking at it whihce
        var moves = ttBestMove != null
            ? new Move[] { ttBestMove.Value }.Concat(board.GetLegalMoves().Where(m => m != ttBestMove.Value))
            : board.GetLegalMoves();
        
        // we check time outside, this secondary check is just to short-circuit the ply
        // if we started it just before running out of time. can be a bit longer that outer value
        if (depth == 0 || moves.Count() == 0 || turnTimer.MillisecondsElapsedThisTurn > 500)
            return (Evaluate(board, moves), Move.NullMove);

        double bestScore = double.NegativeInfinity;
        Move bestMove = Move.NullMove;

        foreach (var nextMove in moves)
        {
            board.MakeMove(nextMove);
            try
            {
                var (eval, _) = Negamax(board, depth - 1, -beta, -alpha);
                eval = -eval;

                if (eval >= bestScore)
                {
                    bestMove = nextMove;
                    bestScore = eval;
                }

                alpha = Math.Max(alpha, eval);
                if (alpha >= beta)
                {
                    cutoffs++;
                    break;
                }
            }
            finally
            {
                board.UndoMove(nextMove);
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

        transpositionTable[zobrist] = new TTEntry(bestScore, ttFlag, bestMove, depth);
        return (bestScore, bestMove);
    }

    // relative to whos turn
    public double Evaluate(Board board, IEnumerable<Move> legalMoves)
    {
        if (board.IsInCheckmate())
            return -100000;

        if (board.IsDraw())
            return 0;

        int bonus = 0;

        // bonus for having lots of mobility, bonus for having > capture available
        bonus += legalMoves.Count() * 1;
        int captures = legalMoves.Count(m => m.IsCapture);
        if (captures > 1)
            bonus += captures * 1;

        var pieces = board
            .GetAllPieceLists()
            .SelectMany(pl => pl);

        //// penalty for own pieces threatened
        //bonus += pieces
        //    // piece color matches color of player, piece's square is attacked by opposing color
        //    .Where(p => p.IsWhite == board.IsWhiteToMove && board.SquareIsAttackedByOpponent(p.Square))
        //    // apply a penalty relative to piece weight
        //    .Select(p => -weights[(int)p.PieceType] / 3)
        //    .Sum();

        //bonus += pieces
        //    // piece color matches color of enemy, piece's square is attacked by opposing color (defended)
        //    .Where(p => p.IsWhite != board.IsWhiteToMove && board.SquareIsAttackedByOpponent(p.Square))
        //    // apply a penalty relative to piece weight
        //    .Select(p => weights[(int)p.PieceType] / 5) // todo: this is fucked, we're giving a BONUS to you if enemy is defensive????
        //    .Sum();

        // todo: also penalize enemy pieces defended by enemy pieces

        // material advantage
        var score = pieces
            .Select(p => weights[(int)p.PieceType] * Sign(p.IsWhite == board.IsWhiteToMove))
            .Sum() + bonus;

        return score;
    }

    void DbgTree(Board board, bool me = true, int depth = 0)
    {
        if (depth > 50) return;

        if (tree.TryGetValue(board.ZobristKey, out var move))
        {
            Console.WriteLine($"{(me ? "me" : "you")} {move}");
            board.MakeMove(move.Item1);
            DbgTree(board, !me, depth + 1);
            board.UndoMove(move.Item1);
            return;
        }

        Console.WriteLine("end!");
    }

    int Sign(bool white) => white ? 1 : -1;
}