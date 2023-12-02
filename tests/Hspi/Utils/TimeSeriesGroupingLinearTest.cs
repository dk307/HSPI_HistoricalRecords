using System.Collections.Generic;
using System.Linq;
using Hspi.Database;
using Hspi.Utils;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class TimeSeriesGroupingLinearTest
    {
        [Test]
        public void EndValuesAreMissingForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 200),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 150),
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 200),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void HalfSampleForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1,  100),
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 300),
                new TimeAndValue(31, 400),
                new TimeAndValue(41, 500),
                new TimeAndValue(51, 600),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 60, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(20, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 200),
                new TimeAndValue(21, 400),
                new TimeAndValue(41, (550 + 600)/2),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void InitialValuesAreMissingForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(11, 250),
                new TimeAndValue(21, 300),
            };

            CollectionAssert.AreEqual(expected, result);
        }

        [Test]
        public void LargeInitialValuesAreMissingForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(115, 200),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 125, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(111, 200),
                new TimeAndValue(121, 200),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void LargeMiddleValuesAreMissingForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 30),
                new TimeAndValue(21, 50),
                new TimeAndValue(131, 655),
                new TimeAndValue(151, 930),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 180, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(30, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, ((40 * 20) + (77.5 * 10))/ 30D),
                new TimeAndValue(31, 187.5),
                new TimeAndValue(61, 352.5),
                new TimeAndValue(91, 517.5),
                new TimeAndValue(121, 737.5),
                new TimeAndValue(151, 930),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void MinStartsAfterLaterInSeriesForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 200),
                new TimeAndValue(10, 300),
                new TimeAndValue(20, 400),
                new TimeAndValue(50, 445),
            };

            TimeSeriesHelper timeSeriesHelper = new(15, 49, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(15, 389.375),
                new TimeAndValue(25, 415),
                new TimeAndValue(35, 430),
                new TimeAndValue(45, 441.25),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void MinValueAfterStartOfSeriesForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 110),
                new TimeAndValue(16, 200),
                new TimeAndValue(26, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(6, 35, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(6, 170),
                new TimeAndValue(16, 250),
                new TimeAndValue(26, 300),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void SimpleIncrementingForLinear()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 300),
                new TimeAndValue(31, 400),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.Linear).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 150),
                new TimeAndValue(11, 250),
                new TimeAndValue(21, 350),
            };

            CollectionAssert.AreEqual(expected, result);
        }
    }
}