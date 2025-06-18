using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class ExceptionHelperTest
    {
        [Test]
        public void SimpleExceptionMessage()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExceptionHelper.GetFullMessage(new Exception("message")), Is.EqualTo("message"));
                Assert.That(ExceptionHelper.GetFullMessage(new ArgumentException("message")), Is.EqualTo("message"));
            });
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
            Assert.That(ExceptionHelper.GetFullMessage(ex, "<BR>"), Is.EqualTo("message<BR>inner exception"));
        }

        [Test]
        public void AggregateExceptionException()
        {
            var exceptions = new List<Exception>() { new("message1"), new("message2") };
            var ex = new AggregateException("message8", exceptions);
            Assert.That(ExceptionHelper.GetFullMessage(ex, "<BR>"), Is.EqualTo("message1<BR>message2"));
        }

        [Test]
        public void IsCancelException()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExceptionHelper.IsCancelException(new TaskCanceledException()));
                Assert.That(ExceptionHelper.IsCancelException(new OperationCanceledException()));
                Assert.That(ExceptionHelper.IsCancelException(new ObjectDisposedException("name")));
                Assert.That(!ExceptionHelper.IsCancelException(new Exception()));
            });
        }
    }
}