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
        500, // Bishop, // 3
        500, // Rook,   // 4
        900, // Queen,  // 5
        0,   // King    // 6
    };

    Random rng = new();
    
    // zobrist -> value, flag {-1 LOWER, 0 EXACT, 1 UPPER}
    Dictionary<ulong, (double, int)> transpositionTable = new();

    public Move Think(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;
        string me = board.IsWhiteToMove ? "White" : "Black";

        for (int ply = 1; timer.MillisecondsElapsedThisTurn < 500; ply++) 
        {
            double bestScoreThisPly = double.NegativeInfinity;
            Move bestMoveThisPly = Move.NullMove;

            foreach (var nextMove in GetMoves(board))
            {
                board.MakeMove(nextMove);
                // pick the move that is worst for the next player
                double eval = -Negamax(board, ply, double.NegativeInfinity, double.PositiveInfinity);
                if (eval >= bestScore)
                {
                    Console.WriteLine($"{me} found better move with score {eval} on ply {ply}");
                    bestMove = nextMove;
                    bestScore = eval;
                }

                if (eval >= bestScoreThisPly)
                {
                    bestScoreThisPly = eval;
                    bestMoveThisPly = nextMove;
                }

                board.UndoMove(nextMove);
            }

            Console.WriteLine($"best this ply: {bestMoveThisPly} for {bestScore} on ply {ply}");
        }
        Console.WriteLine("\n========");
        return bestMove;
    }

    // https://en.wikipedia.org/wiki/Negamax#Negamax_with_alpha_beta_pruning_and_transposition_tables
    double Negamax(
        Board board,
        int depth,
        double alpha,
        double beta)
    {
        double originalAlpha = alpha;

        if (transpositionTable.TryGetValue(board.ZobristKey, out var ttEntry))
        {
            switch (ttEntry.Item2) {
                case 0:
                    return ttEntry.Item1;
                case -1:
                    alpha = Math.Max(alpha, ttEntry.Item1);
                    break;
                case 1:
                    beta = Math.Max(beta, ttEntry.Item1);
                    break;
            }
        }    

        if (depth == 0 || board.IsInCheckmate() || board.IsDraw())
            return Evaluate(board);

        double value = double.NegativeInfinity;
        foreach (var nextMove in GetMoves(board))
        {
            board.MakeMove(nextMove);
            try
            {
                value = Math.Max(value, -Negamax(board, depth - 1, -beta, -alpha));
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

        transpositionTable[board.ZobristKey] = (value, ttFlag);

        return value;
    }

    // relative to whos turn it is, high = good, low = bad
    public double Evaluate(Board board)
    {
        // checkmake is always back on your turn
        if (board.IsInCheckmate())
            return -10000;

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