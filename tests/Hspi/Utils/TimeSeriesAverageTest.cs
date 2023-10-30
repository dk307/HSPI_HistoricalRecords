using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class TimeSeriesAverageTest
    {
        [TestMethod]
        public void AverageForEmptyList()
        {
            List<TimeAndValue> dbValues = new()
            {
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 100, dbValues);
            var result = timeSeriesHelper.Average(FillStrategy.Linear);

            Assert.IsFalse(result.HasValue);
        }

        [TestMethod]
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

            Assert.AreEqual(((150D * 15) + (10 * 250) + (75 * 300)) / 100D, result);
        }

        [TestMethod]
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

            Assert.AreEqual(((100D * 15) + (10 * 200) + (75 * 300)) / 100D, result);
        }

        [TestMethod]
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

            Assert.AreEqual(((100D * 10) + (10 * 200) + (75 * 300)) / 95D, result);
        }
    }
}