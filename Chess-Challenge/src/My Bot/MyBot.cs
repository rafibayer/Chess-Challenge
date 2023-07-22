﻿using ChessChallenge.API;
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

    Random rng = new();

    public Move Think(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;

        string me = board.IsWhiteToMove ? "White" : "Black";

        foreach (var nextMove in GetMoves(board))
        {
            board.MakeMove(nextMove);
            double eval = Negamax(board, 4, double.NegativeInfinity, double.PositiveInfinity, Sign(board.IsWhiteToMove), timer);
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
        int color,
        Timer timer)
    {
        if (board.IsInCheckmate() || board.IsDraw() || timer.MillisecondsElapsedThisTurn > 1000)
            return color * Evaluate(board);

        double value = double.NegativeInfinity;
        foreach (var nextMove in GetMoves(board))
        {
            board.MakeMove(nextMove);

            try
            {
                value = Math.Max(value, -Negamax(board, depth - 1, -beta, -alpha, -color, timer));
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
            return 100000 * -Sign(board.IsWhiteToMove == board.IsWhiteToMove);

        return board
            .GetAllPieceLists()
            .SelectMany(pl => pl)
            .Select(p => values[(int)p.PieceType] * Sign(p.IsWhite == board.IsWhiteToMove))
            .Sum();
    }

    int Sign(bool white) => white ? 1 : -1;

    IEnumerable<Move> GetMoves(Board board) => board.GetLegalMoves().OrderBy(_ => rng.NextDouble());

    // if we call enough times, this is a token saver?
    //double Max(double a, double b) => Math.Max(a, b);
}