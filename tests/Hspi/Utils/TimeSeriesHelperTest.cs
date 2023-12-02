using System;
using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class TimeSeriesHelperTest
    {
        [Test]
        public void ConstructorThrowsExceptionWhenMinIsGreaterThanMax()
        {
            // Arrange
            long minUnixTimeSeconds = 100;
            long maxUnixTimeSeconds = 10;
            IList<TimeAndValue> list = new List<TimeAndValue>();

            // Act & Assert
            Assert.Catch<ArgumentOutOfRangeException>(() =>
            {
                new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, list);
            });
        }

        [Test]
        public void ThrowsExceptionWhenIntervalIsZero()
        {
            // Arrange
            long minUnixTimeSeconds = 0;
            long maxUnixTimeSeconds = 100;
            IList<TimeAndValue> list = new List<TimeAndValue>();

            Assert.Catch<ArgumentOutOfRangeException>(() =>
            {
                var ts = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, list);
                ts.ReduceSeriesWithAverage(0, FillStrategy.LOCF);
            });
        }
    }
}