using ChessChallenge.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Challenge.test
{
    [TestClass]
    public class MyBotTests
    {
        ChessChallenge.API.Timer Inf => new ChessChallenge.API.Timer(int.MaxValue);

        const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        [TestMethod]
        public void WhiteMaterialAdvantageTest()
        {
            MyBot bot = new MyBot();
            string whiteMaterialAdvantageFEN = "3k4/8/8/8/8/8/8/NNNNKNNN w - - 0 1";

            Board whiteToPlay = Board.CreateBoardFromFEN(whiteMaterialAdvantageFEN);
            Assert.IsTrue(bot.Evaluate(whiteToPlay, whiteToPlay.GetLegalMoves()) > 0);

            Board blackToPlay = Board.CreateBoardFromFEN(whiteMaterialAdvantageFEN.Replace(" w ", " b "));
            Assert.IsTrue(bot.Evaluate(blackToPlay, whiteToPlay.GetLegalMoves()) < 0);
        }

        [TestMethod]
        public void QueenTakeTest()
        {
            Repeat(100, () =>
            {
                MyBot bot = new MyBot();
                string takeQueenFEN = "k7/8/8/3q4/4Q3/8/8/7K w - - 0 1";

                Board whiteToPlay = Board.CreateBoardFromFEN(takeQueenFEN);
                Move best = bot.Think(whiteToPlay, Inf);
                Assert.AreEqual(new Move("e4d5", whiteToPlay), best);

                Board blackToPlay = Board.CreateBoardFromFEN(takeQueenFEN.Replace(" w ", " b "));
                best = bot.Think(blackToPlay, Inf);
                Assert.AreEqual(new Move("d5e4", whiteToPlay), best);
            });
        }

        [TestMethod]
        public void EvaluateSymmetryTest()
        {
            MyBot bot = new MyBot();

            string fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPP1PPP/RNBQKBNR w KQkq - 0 1\r\n";
            Board board = Board.CreateBoardFromFEN(fen);

            bot.Evaluate(board, board.GetLegalMoves());

            string cur = board.IsWhiteToMove ? " w " : " b ";
            string next = board.IsWhiteToMove ? " b " : " w ";

            board = Board.CreateBoardFromFEN(fen.Replace(cur, next));
            bot.Evaluate(board, board.GetLegalMoves());
        }


        void Repeat(int n, Action f)
        {
            Parallel.For(0, n, (_) => f());
        }
    }
}
