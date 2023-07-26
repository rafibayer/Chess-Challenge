using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// MyBot @cdeb87ff7b1dd2db81ef92d1817d3ed35a4904fd
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
    Dictionary<ulong, (double, int, Move)> transpositionTable = new();

    Dictionary<(ulong, Move), double> moveScoreCache = new();

    int cutoffs = 0;

    public Move Think(Board board, Timer timer)
    {
        Console.WriteLine(board.GetFenString());
        turnTimer = timer;
        //moveScoreCache.Clear();

        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;

        // need to stop special casing root, we need a more
        // elegant way to get the move associated with an evaluation
        for (int ply = 2; turnTimer.MillisecondsElapsedThisTurn < 500; ply++)
        {
            var (eval, move) = Negamax(board, ply, double.NegativeInfinity, double.PositiveInfinity);
            if (eval > bestScore)
            {
                bestScore = eval;
                bestMove = move;
                Console.WriteLine($"{(board.IsWhiteToMove ? "White" : "Black")} found {bestMove} ~ {bestScore} on ply {ply}");
            }
        }

        Console.WriteLine($"\n==== cutoffs {cutoffs} ====");
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

        if (transpositionTable.TryGetValue(zobrist, out var ttEntry))
        {
            var (tteValue, tteFlag, tteMove) = ttEntry;
            switch (tteFlag)
            {
                case 0:
                    return (tteValue, tteMove);
                case -1:
                    alpha = Math.Max(alpha, tteValue);
                    break;
                case 1:
                    beta = Math.Min(beta, tteValue);
                    break;
            }
        }

        var moves = board.GetLegalMoves()
            .OrderBy(m => moveScoreCache.TryGetValue((zobrist, m), out var cachedScore) ? -cachedScore : rng.NextDouble());

        // we check time outside, this secondary check is just to short-circuit the ply
        // if we started it just before running out of time. can be a bit longer that outer value
        if (depth == 0 || turnTimer.MillisecondsElapsedThisTurn > 500 || board.IsInCheckmate() || board.IsDraw())
            return (Evaluate(board, moves), Move.NullMove);

        double bestScore = double.NegativeInfinity;
        Move bestMove = Move.NullMove;

        foreach (var nextMove in moves)
        {
            board.MakeMove(nextMove);
            try
            {
                var (eval, move) = Negamax(board, depth - 1, -beta, -alpha);
                eval = -eval;

                if (eval > bestScore)
                {
                    bestMove = nextMove;
                    bestScore = eval;
                }

                moveScoreCache[(zobrist, move)] = eval;

                alpha = Math.Max(alpha, bestScore);
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

        int ttFlag = 0;
        if (bestScore <= originalAlpha)
            ttFlag = 1;
        else if (bestScore >= beta)
            ttFlag = -1;

        transpositionTable[zobrist] = (bestScore, ttFlag, bestMove);

        return (bestScore, bestMove);
    }

    // relative to whos turn it is, high = good, low = bad
    public double Evaluate(Board board, IEnumerable<Move> legalMoves)
    {
        // checkmake is always back on your turn
        if (board.IsInCheckmate())
            return -100000;

        if (board.IsDraw())
            return 0;

        int bonus = 0;

        //// bonus for having lots of mobility, bonus for having > capture available
        //bonus += legalMoves.Count() * 3;
        //int captures = legalMoves.Count(m => m.IsCapture);
        //if (captures > 1)
        //    bonus += captures * 4;

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

    int Sign(bool white) => white ? 1 : -1;
}