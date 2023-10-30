using System;
using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class TimeSeriesHelperTest
    {
        [TestMethod]
        public void ConstructorThrowsExceptionWhenMinIsGreaterThanMax()
        {
            // Arrange
            long minUnixTimeSeconds = 100;
            long maxUnixTimeSeconds = 10;
            IList<TimeAndValue> list = new List<TimeAndValue>();

            // Act & Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, list);
            });
        }

        [TestMethod]
        public void ThrowsExceptionWhenIntervalIsZero()
        {
            // Arrange
            long minUnixTimeSeconds = 0;
            long maxUnixTimeSeconds = 100;
            IList<TimeAndValue> list = new List<TimeAndValue>();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                var ts = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, list);
                ts.ReduceSeriesWithAverage(0, FillStrategy.LOCF);
            });
        }
    }
}