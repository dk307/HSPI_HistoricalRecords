using Hspi.Device;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class PeriodHumanizerTest

    {
        [TestCase(31536000UL, "Last - 365 days")] // 1 year
        [TestCase(60UL * 60 * 2, "Last - 2 hours")]
        [TestCase(60UL, "Last - 1 minute")]
        [TestCase(60UL * 60 * 24 * 7, "Last - 7 days")]
        public void CalculateMinMaxSecondsForLastXDuration(ulong count, string expected)
        {
            var period = new Period(null, new Instant(InstantType.Now), count);
            Assert.That(period.Humanize(), Is.EqualTo(expected));
        }
    }
}