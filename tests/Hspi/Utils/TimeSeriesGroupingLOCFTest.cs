using System.Collections.Generic;
using System.Linq;
using Hspi.Database;
using Hspi.Utils;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class TimeSeriesGroupingLOCFTest
    {
        [Test]
        public void AlreadyCorrectGroupingForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.LOCF).ToArray();

            CollectionAssert.AreEqual(dbValues, result);
        }

        [Test]
        public void EndValuesAreMissingForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 200),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 200),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void HalfSampleForLOCF()
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
            var result = timeSeriesHelper.ReduceSeriesWithAverage(20, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 150),
                new TimeAndValue(21, 350),
                new TimeAndValue(41, 550),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void InitialValuesAreMissingForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.LOCF).ToArray();

            CollectionAssert.AreEqual(dbValues, result);
        }

        [Test]
        public void LargeInitialValuesAreMissingForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(115, 200),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 125, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(111, 200),
                new TimeAndValue(121, 200),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void LargeMiddleValuesAreMissingForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 10),
                new TimeAndValue(21, 20),
                new TimeAndValue(131, 600),
                new TimeAndValue(151, 900),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 180, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(30, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, ((20D*10) + (20 *10))/30D),
                new TimeAndValue(31, 20),
                new TimeAndValue(61, 20),
                new TimeAndValue(91, 20),
                new TimeAndValue(121, ((20D * 10) + (20 * 600))/30D),
                new TimeAndValue(151, 900),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void MiddleValuesAreMissingForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(41, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 60, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 100),
                new TimeAndValue(21, 100),
                new TimeAndValue(31, 100),
                new TimeAndValue(41, 300),
                new TimeAndValue(51, 300),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void MinStartsAfterLaterInSeriesForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 200),
                new TimeAndValue(10, 300),
                new TimeAndValue(20, 400),
                new TimeAndValue(50, 500),
            };

            TimeSeriesHelper timeSeriesHelper = new(15, 49, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(15, ((5 * 300) + (5* 400)) / 10),
                new TimeAndValue(25, 400),
                new TimeAndValue(35, 400),
                new TimeAndValue(45, 400),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void MinValueAfterStartOfSeriesForLOCF()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(16, 200),
                new TimeAndValue(26, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(6, 35, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(10, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(6, 100),
                new TimeAndValue(16, 200),
                new TimeAndValue(26, 300),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void ReduceSampleMisc1()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1,  100),
                new TimeAndValue(21, 300),
                new TimeAndValue(26, 350),
                new TimeAndValue(31, 400),
                new TimeAndValue(41, 500),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 60, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(20, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(21, ((5*300D) + (5*350) + (10*400)) /20D ),
                new TimeAndValue(41, 500),
            };

            CollectionAssert.AreEqual(result, expected);
        }

        [Test]
        public void UpSampleMisc()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(5, 300),
                new TimeAndValue(9, 350),
                new TimeAndValue(10, 400),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 15, dbValues);
            var result = timeSeriesHelper.ReduceSeriesWithAverage(1, FillStrategy.LOCF).ToArray();

            List<TimeAndValue> expected = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(2, 100),
                new TimeAndValue(3, 100),
                new TimeAndValue(4, 100),
                new TimeAndValue(5, 300),
                new TimeAndValue(6, 300),
                new TimeAndValue(7, 300),
                new TimeAndValue(8, 300),
                new TimeAndValue(9, 350),
                new TimeAndValue(10, 400),
                new TimeAndValue(11, 400),
                new TimeAndValue(12, 400),
                new TimeAndValue(13, 400),
                new TimeAndValue(14, 400),
                new TimeAndValue(15, 400),
            };

            CollectionAssert.AreEqual(result, expected);
        }
    }
}