using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// MyBot @a1b14cd3d706ba672e93190b7bec50d9fb770f8b
public class EvilBot : IChessBot
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

    static int[] weights = new int[]
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

    public Move Think(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;

        string me = board.IsWhiteToMove ? "White" : "Black";

        foreach (var nextMove in GetMoves(board))
        {
            board.MakeMove(nextMove);
            // pick the move that is worst for the next player
            double eval = -Negamax(board, 4, double.NegativeInfinity, double.PositiveInfinity, timer);
            if (eval > bestScore)
            {
                Console.WriteLine($"{me} found better move with score {eval}");
                bestMove = nextMove;
                bestScore = eval;
            }

            board.UndoMove(nextMove);
        }

        Console.WriteLine("\n========");
        return bestMove;
    }

    // https://en.wikipedia.org/wiki/Negamax#Negamax_with_alpha_beta_pruning
    double Negamax(
        Board board,
        int depth,
        double alpha,
        double beta,
        Timer timer)
    {
        if (depth == 0 || board.IsInCheckmate() || board.IsDraw() || timer.MillisecondsElapsedThisTurn > 500)
            return Evaluate(board);

        double value = double.NegativeInfinity;
        foreach (var nextMove in GetMoves(board))
        {
            board.MakeMove(nextMove);
            try
            {
                // 0.9 discount factor, near states are better than far states
                value = Math.Max(value, 0.9 * -Negamax(board, depth - 1, -beta, -alpha, timer));
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

        return value;
    }

    // relative to whos turn it is
    public double Evaluate(Board board)
    {
        if (board.IsInCheckmate())
            return -10000;

        return board
            .GetAllPieceLists()
            .SelectMany(pl => pl)
            .Select(p => weights[(int)p.PieceType] * Sign(p.IsWhite == board.IsWhiteToMove))
            .Sum() + (Sign(board.IsInCheck()) * -500);
    }

    int Sign(bool white) => white ? 1 : -1;

    IEnumerable<Move> GetMoves(Board board) => board.GetLegalMoves().OrderBy(_ => rng.NextDouble());
}