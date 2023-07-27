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

        [TestMethod]
        public void WhiteMaterialAdvantageTest()
        {
            MyBot bot = new MyBot();
            string whiteMaterialAdvantageFEN = "3k4/8/8/8/8/8/8/NNNNKNNN w - - 0 1";

            Board whiteToPlay = Board.CreateBoardFromFEN(whiteMaterialAdvantageFEN);
            Assert.IsTrue(bot.Evaluate(whiteToPlay) > 0);

            // we're testing non-relative eval
            //Board blackToPlay = Board.CreateBoardFromFEN(whiteMaterialAdvantageFEN.Replace(" w ", " b "));
            //Assert.IsTrue(bot.Evaluate(blackToPlay) < 0);
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

        void Repeat(int n, Action f)
        {
            Parallel.For(0, n, (_) => f());
        }
    }
}
