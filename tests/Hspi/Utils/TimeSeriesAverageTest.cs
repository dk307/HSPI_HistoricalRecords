using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoryTest
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
        public void AverageForLOCF2()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(100 - 10 * 60, 10),
                new TimeAndValue(1000 - 9 * 60, 10),
                new TimeAndValue(1000 - 5 * 60, 50),
            };

            TimeSeriesHelper timeSeriesHelper = new(1000 - 10 * 60, 1000, dbValues);
            var result = timeSeriesHelper.Average(FillStrategy.LOCF);

            Assert.That(result, Is.EqualTo(((10D * 5 * 61) + (50D * 5 * 60)) / 601D));
        }

        [Test]
        public void AverageForLinear2()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(100 - 10 * 60, 10),
                new TimeAndValue(1000 - 9 * 60, 10),
                new TimeAndValue(1000 - 5 * 60, 50),
            };

            TimeSeriesHelper timeSeriesHelper = new(1000 - 10 * 60, 1000, dbValues);
            var result = timeSeriesHelper.Average(FillStrategy.Linear);

            Assert.That(result, Is.EqualTo(((10D * 1 * 60) + (30D * 4 * 60) + (50D * 301)) / 601D));
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