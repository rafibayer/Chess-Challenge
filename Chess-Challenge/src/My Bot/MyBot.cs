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
        300, // Bishop, // 3
        500, // Rook,   // 4
        900, // Queen,  // 5
        0,   // King    // 6
    };

    Random rng = new();
    Timer turnTimer;
    
    // zobrist, depth -> value, flag {-1 LOWER, 0 EXACT, 1 UPPER}, Move
    Dictionary<(ulong, int), (double, int, Move)> transpositionTable = new();

    public Move Think(Board board, Timer timer)
    {
        turnTimer = timer;

        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;

        // need to stop special casing root, we need a more
        // elegant way to get the move associated with an evaluation
        for (int ply = 1; turnTimer.MillisecondsElapsedThisTurn < 600; ply++) 
        {
            var (eval, move) = Negamax(board, ply, double.NegativeInfinity, double.PositiveInfinity);
            if (eval > bestScore)
            {
                bestScore = eval;
                bestMove = move;
                Console.WriteLine($"{(board.IsWhiteToMove ? "White" : "Black")} found better move with score {bestScore} on ply {ply}");
            }
        }

        Console.WriteLine("\n========");
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

        if (transpositionTable.TryGetValue((board.ZobristKey, depth), out var ttEntry))
        {
            var (tteValue, tteFlag, tteMove) = ttEntry;
            switch (tteFlag) {
                case 0:
                    return (tteValue, tteMove);
                case -1:
                    alpha = Math.Max(alpha, tteValue);
                    break;
                case 1:
                    beta = Math.Max(beta, tteValue);
                    break;
            }
        }    

        // we check time outside, this secondary check is just to short-circuit the ply
        // if we started it just before running out of time. can be a bit longer that outer value
        if (depth == 0 || turnTimer.MillisecondsElapsedThisTurn > 750 || board.IsInCheckmate() || board.IsDraw())
            return (Evaluate(board), Move.NullMove);

        double value = double.NegativeInfinity;
        Move bestMove = Move.NullMove;

        foreach (var nextMove in GetMoves(board))
        {
            board.MakeMove(nextMove);
            try
            {
                var result = Negamax(board, depth - 1, -beta, -alpha);
                var eval = -result.Item1;
                if (eval > value)
                {
                    bestMove = nextMove;
                    value = eval;
                }

                alpha = Math.Max(alpha, value);
                if (alpha >= beta)
                {
                    break;
                }
            }
            finally
            {
                board.UndoMove(nextMove);
            }
        }

        int ttFlag = 0;
        if (value <= originalAlpha)
            ttFlag = 1;
        else if (value >= beta)
            ttFlag = -1;

        transpositionTable[(board.ZobristKey, depth)] = (value, ttFlag, bestMove);

        return (value, bestMove);
    }

    // relative to whos turn it is, high = good, low = bad
    public double Evaluate(Board board)
    {
        // checkmake is always back on your turn
        if (board.IsInCheckmate())
            return -100000;

        // material advantage, penalty for being in check
        return board
            .GetAllPieceLists()
            .SelectMany(pl => pl)
            .Select(p => weights[(int)p.PieceType] * Sign(p.IsWhite == board.IsWhiteToMove))
            .Sum() + (Sign(board.IsInCheck()) * -200);
    }

    int Sign(bool white) => white ? 1 : -1;

    IEnumerable<Move> GetMoves(Board board) => board.GetLegalMoves().OrderBy(_ => rng.NextDouble());
}