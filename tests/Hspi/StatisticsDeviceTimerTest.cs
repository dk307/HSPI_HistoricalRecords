using System;
using System.Threading;
using Hspi.Device;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class StatisticsDeviceTimerTest

    {
        [Test]
        public void TimerCalledForNormalCase()
        {
            var dt = new DateTimeOffset(2022, 5, 3, 1, 2, 3, 0, TimeSpan.FromHours(8));

            FakeGlobalClock fakeGlobalClock = new(DateTimeOffset.MinValue, dt, DayOfWeek.Monday);

            ManualResetEvent called = new(false);

            var period = new StatisticsDeviceTimer(fakeGlobalClock,
                                                   Period.CreatePastInterval(100),
                                                   1,
                                                   () => called.Set(),
                                                   CancellationToken.None);
            Assert.That(called.WaitOne(), Is.True);
        }

        [Test]
        public void TimerCalledForPredefined([Values] PreDefinedPeriod preDefinedPeriod)
        {
            var dt = new DateTimeOffset(2022, 5, 3, 23, 59, 59, 999, TimeSpan.FromHours(8));

            TestPredefinedTimer(preDefinedPeriod, dt);
        }

        private static void TestPredefinedTimer(PreDefinedPeriod preDefinedPeriod, DateTimeOffset dt)
        {
            FakeGlobalClock fakeGlobalClock = new(DateTimeOffset.MinValue, dt, DayOfWeek.Monday);

            ManualResetEvent called = new(false);

            var period = new StatisticsDeviceTimer(fakeGlobalClock,
                                                   Period.Create(preDefinedPeriod),
                                                   999999,
                                                   () => called.Set(),
                                                   CancellationToken.None);
            Assert.That(called.WaitOne(), Is.True);
        }

        [Test]
        public void TimerCalledForThisHour()
        {
            var dt = new DateTimeOffset(2022, 5, 3, 1, 59, 59, 999, TimeSpan.FromHours(8));
            TestPredefinedTimer(PreDefinedPeriod.ThisHour, dt);
        }
    }
}