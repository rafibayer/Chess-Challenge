using ChessChallenge.API;
using ChessChallenge.Chess;
using System.Collections.Concurrent;
using API = ChessChallenge.API;
using Chess = ChessChallenge.Chess;

namespace Chess_Challenge_Runner
{
    struct RunnerResult
    {
        public GameResult GameResult { get; set; }

        public API.Move[] Moves { get; set; }
    }

    public class Program
    {
        const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        static void Main(string[] args)
        {
            ConcurrentBag<GameResult> results = new();

            Parallel.For(0, 5, (i) =>
            {
                Console.WriteLine($"starting game {i}");
                results.Add(PlayGame(() => new MyBot(), () => new EvilBot()));
                Console.WriteLine($"finished game {i}");
            });

            foreach (var result in results)
            {
                Console.WriteLine(result);
            }
        }

        static GameResult PlayGame(Func<API.IChessBot> playerAFactory, Func<API.IChessBot> playerBFactory)
        {
            Chess.Board board = new();
            board.LoadStartPosition();

            var playerA = playerAFactory();
            var playerB = playerBFactory();

            bool white = true;

            GameResult result = GameResult.InProgress;
            while (result == Chess.GameResult.InProgress)
            {
                var active = white ? playerA : playerB;
                var move = active.Think(new (board), new(int.MaxValue));
                board.MakeMove(new (move.RawValue));

                white = !white;
                result = Arbiter.GetGameState(board);
            }

            return result;
        }
    }
}