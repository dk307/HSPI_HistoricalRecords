using System;
using System.Collections.Generic;
using System.Linq;
using Hspi;
using Hspi.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class TimeSeriesHelperTest
    {
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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