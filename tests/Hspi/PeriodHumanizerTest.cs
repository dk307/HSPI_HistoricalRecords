using Hspi.Device;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class PeriodHumanizerTest

    {
        [TestCase(31536000UL, "Last - 365 days")] // 1 year
        [TestCase(60UL * 60 * 2, "Last - 2 hours")]
        [TestCase(60UL, "Last - 1 minute")]
        [TestCase(60UL * 60 * 24 * 7, "Last - 7 days")]
        public void CalculateTextForPastInterval(ulong count, string expected)
        {
            var period = new StatisticsFunctionDuration(Period.CreatePastInterval(count));
            Assert.That(period.Humanize(), Is.EqualTo(expected));
        }

        [TestCase(PreDefinedPeriod.Today, "Today")]
        [TestCase(PreDefinedPeriod.Yesterday, "Yesterday")]
        [TestCase(PreDefinedPeriod.ThisWeek, "This week")]
        [TestCase(PreDefinedPeriod.ThisMonth, "This month")]
        [TestCase(PreDefinedPeriod.ThisHour, "This hour")]
        public void CalculateTextForPredefined(PreDefinedPeriod preDefinedPeriod, string expected)
        {
            var period = new StatisticsFunctionDuration(preDefinedPeriod);
            Assert.That(period.Humanize(), Is.EqualTo(expected));
        }
    }
}