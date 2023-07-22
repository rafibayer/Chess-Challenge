using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

public class MyBot : IChessBot
{
    static int[] values = new int[]
    {
        0,   // None,   // 0
        100, // Pawn,   // 1
        300, // Knight, // 2
        500, // Bishop, // 3
        500, // Rook,   // 4
        900, // Queen,  // 5
        0,   // King    // 6
    };

    public Move Think(Board board, Timer timer)
    {
        // multiply score by sign so we are always maximizing here?
        // makes logic branchless, saves tokens?
        double value = double.NegativeInfinity;
        Move best = Move.NullMove;
        foreach (var move in board.GetLegalMoves()) 
        { 
            board.MakeMove(move);
            double eval = Negamax(board, 5, double.NegativeInfinity, double.PositiveInfinity, timer);
            if (eval > value)
            {
                value = eval;
                best = move;
            }

            board.UndoMove(move);
        }

        return best;
    }

    // https://en.wikipedia.org/wiki/Negamax#Negamax_with_alpha_beta_pruning
    double Negamax(
        Board board,
        int depth,
        double alpha,
        double beta,
        Timer timer)
    {
        // double check this, who is checkmaket here?
        if (timer.MillisecondsElapsedThisTurn > 300 || board.IsInCheckmate())
            return Evaluate(board);

        double value = double.NegativeInfinity;
        foreach (var nextMove in board.GetLegalMoves())
        {
            board.MakeMove(nextMove);
            value = Math.Max(value, -Negamax(board, depth-1, -beta, -alpha, timer));
            alpha = Math.Max(alpha, value);
            if (alpha >= beta)
            {
                board.UndoMove(nextMove);
                break;
            }

            board.UndoMove(nextMove);
        }

        return value;
    }

    // white is maximizing:
    // high score = good for white, low score = good for black
    double Evaluate(Board board)
    {
        if (board.IsInCheckmate())
            return 10000 * -Sign(board.IsWhiteToMove);

        // material evaluation, sum of white piece values - sum of black piece values
        return board
            .GetAllPieceLists()
            .SelectMany(pl => pl)
            .Select(p => values[(int)p.PieceType] * Sign(p.IsWhite))
            .Sum();
    }

    int Sign(bool white) => white ? 1 : -1;

    // if we call enough times, this is a token saver?
    //double Max(double a, double b) => Math.Max(a, b);
}