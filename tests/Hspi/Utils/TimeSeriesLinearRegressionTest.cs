using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    // used for testing : https://www.statskingdom.com/linear-regression-calculator.html
    [TestFixture]
    public class TimeSeriesLinearRegressionTest
    {
        [Test]
        public void AverageForEmptyList()
        {
            List<TimeAndValue> dbValues = new()
            {
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 100, dbValues);
            var result = timeSeriesHelper.CalculateLinearRegression();
            Assert.That(result, Is.EqualTo(0));
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
            var result = timeSeriesHelper.CalculateLinearRegression();
            Assert.That(result, Is.EqualTo(7.8947368421052637d));
        }

        [Test]
        public void AverageForSingleValueList()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 100, dbValues);
            var result = timeSeriesHelper.CalculateLinearRegression();
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void MultipleRepeatedValues()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 10),
                new TimeAndValue(21, 20),
                new TimeAndValue(31, 10),
                new TimeAndValue(131, 600),
                new TimeAndValue(151, 20),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 180, dbValues);
            var result = timeSeriesHelper.CalculateLinearRegression();
            Assert.That(result, Is.EqualTo(2.0158562367864694d));
        }
    }
}