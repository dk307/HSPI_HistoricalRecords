using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class ExceptionHelperTest
    {
        [Test]
        public void SimpleExceptionMessage()
        {
            Assert.That("message", Is.EqualTo(ExceptionHelper.GetFullMessage(new Exception("message"))));
            Assert.That("message", Is.EqualTo(ExceptionHelper.GetFullMessage(new ArgumentException("message"))));
        }

        [Test]
        public void InnerExceptionMessage()
        {
            var ex = new Exception("message", new Exception("inner exception"));
            Assert.That(ExceptionHelper.GetFullMessage(ex), Is.EqualTo("message" + Environment.NewLine + "inner exception"));

            var ex2 = new Exception("message2", ex);
            Assert.That(ExceptionHelper.GetFullMessage(ex2), Is.EqualTo("message2" + Environment.NewLine + "message" + Environment.NewLine + "inner exception"));
        }

        [Test]
        public void InnerExceptionMessagesAreCollapsed()
        {
            var ex = new Exception("message", new Exception("inner exception"));

            var ex2 = new Exception("Message", ex);
            Assert.That(ExceptionHelper.GetFullMessage(ex2), Is.EqualTo("Message" + Environment.NewLine + "message" + Environment.NewLine + "inner exception"));
        }

        [Test]
        public void MessageWithEOL()
        {
            var ex = new Exception("message", new Exception("inner exception"));
            Assert.That("message<BR>inner exception", Is.EqualTo(ExceptionHelper.GetFullMessage(ex, "<BR>")));
        }

        [Test]
        public void AggregateExceptionException()
        {
            var exceptions = new List<Exception>() { new("message1"), new("message2") };
            var ex = new AggregateException("message8", exceptions);
            Assert.That("message1<BR>message2", Is.EqualTo(ExceptionHelper.GetFullMessage(ex, "<BR>")));
        }

        [Test]
        public void IsCancelException()
        {
            Assert.That(ExceptionHelper.IsCancelException(new TaskCanceledException()));
            Assert.That(ExceptionHelper.IsCancelException(new OperationCanceledException()));
            Assert.That(ExceptionHelper.IsCancelException(new ObjectDisposedException("name")));
            Assert.That(!ExceptionHelper.IsCancelException(new Exception()));
        }
    }
}