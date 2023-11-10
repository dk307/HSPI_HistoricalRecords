using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class ExceptionHelperTest
    {
        [TestMethod]
        public void SimpleExceptionMessage()
        {
            Assert.AreEqual("Message", ExceptionHelper.GetFullMessage(new Exception("message")));
            Assert.AreEqual("Message", ExceptionHelper.GetFullMessage(new ArgumentException("message")));
        }

        [TestMethod]
        public void InnerExceptionMessage()
        {
            var ex = new Exception("message", new Exception("inner exception"));
            Assert.AreEqual(ExceptionHelper.GetFullMessage(ex), "Message" + Environment.NewLine + "Inner exception");

            var ex2 = new Exception("message2", ex);
            Assert.AreEqual(ExceptionHelper.GetFullMessage(ex2), "Message 2" + Environment.NewLine + "Message" + Environment.NewLine + "Inner exception");
        }

        [TestMethod]
        public void InnerExceptionMessagesAreCollapsed()
        {
            var ex = new Exception("message", new Exception("inner exception"));

            var ex2 = new Exception("Message", ex);
            Assert.AreEqual(ExceptionHelper.GetFullMessage(ex2), "Message" + Environment.NewLine + "Inner exception");
        }

        [TestMethod]
        public void MessageWithEOL()
        {
            var ex = new Exception("message", new Exception("inner exception"));
            Assert.AreEqual("Message<BR>Inner exception", ExceptionHelper.GetFullMessage(ex, "<BR>"));
        }

        [TestMethod]
        public void AggregateExceptionException()
        {
            var exceptions = new List<Exception>() { new Exception("message1"), new Exception("message2") };
            var ex = new AggregateException("message8", exceptions);
            Assert.AreEqual("Message 1<BR>Message 2", ExceptionHelper.GetFullMessage(ex, "<BR>"));
        }

        [TestMethod]
        public void IsCancelException()
        {
            Assert.IsTrue(ExceptionHelper.IsCancelException(new TaskCanceledException()));
            Assert.IsTrue(ExceptionHelper.IsCancelException(new OperationCanceledException()));
            Assert.IsTrue(ExceptionHelper.IsCancelException(new ObjectDisposedException("name")));
            Assert.IsFalse(ExceptionHelper.IsCancelException(new Exception()));
        }
    }
}