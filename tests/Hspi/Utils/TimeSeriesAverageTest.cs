using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class TimeSeriesAverageTest
    {
        [Test]
        public void AverageForEmptyList()
        {
            List<TimeAndValue> dbValues = new()
            {
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 100, dbValues);
            var result = timeSeriesHelper.Average(FillStrategy.Linear);

            Assert.That(!result.HasValue);
        }

        [Test]
        public void AverageForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(16, 200),
                new TimeAndValue(26, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 100, dbValues);
            var result = timeSeriesHelper.Average(FillStrategy.Linear);

            Assert.That(result, Is.EqualTo(((150D * 15) + (10 * 250) + (75 * 300)) / 100D));
        }

        [Test]
        public void AverageForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(16, 200),
                new TimeAndValue(26, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 100, dbValues);
            var result = timeSeriesHelper.Average(FillStrategy.LOCF);

            Assert.That(result, Is.EqualTo(((100D * 15) + (10 * 200) + (75 * 300)) / 100D));
        }

        [Test]
        public void AverageStartsLaterForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(16, 200),
                new TimeAndValue(26, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(6, 100, dbValues);
            var result = timeSeriesHelper.Average(FillStrategy.LOCF);

            Assert.That(result, Is.EqualTo(((100D * 10) + (10 * 200) + (75 * 300)) / 95D));
        }
    }
}