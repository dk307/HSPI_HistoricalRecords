using System.Collections.Generic;
using System.Linq;
using Hspi.Database;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class TimeSeriesGroupingHistogramTest
    {
        [TestMethod]
        public void EndValuesAreMissing()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 200),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.CreateHistogram();

            Dictionary<double, long> expected = new()
            {
                {100, 10},
                {200, 20},
            };

            CollectionAssert.AreEqual(expected.ToArray(), result.ToArray());
        }

        [TestMethod]
        public void EqualDividedValues()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 100),
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.CreateHistogram();

            Dictionary<double, long> expected = new()
            {
                {100, 10},
                {200, 10},
                {300, 10},
            };

            CollectionAssert.AreEqual(expected.ToArray(), result.ToArray());
        }

        [TestMethod]
        public void InitialValuesAreMissing()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(11, 200),
                new TimeAndValue(21, 300),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 30, dbValues);
            var result = timeSeriesHelper.CreateHistogram();

            Dictionary<double, long> expected = new()
            {
                {200, 10},
                {300, 10},
            };

            CollectionAssert.AreEqual(expected.ToArray(), result.ToArray());
        }

        [TestMethod]
        public void LargeInitialValuesAreMissing()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(115, 200),
            };

            TimeSeriesHelper timeSeriesHelper = new(1, 125, dbValues);
            var result = timeSeriesHelper.CreateHistogram();

            Dictionary<double, long> expected = new()
            {
               {200, 11},
            };

            CollectionAssert.AreEqual(expected.ToArray(), result.ToArray());
        }

        [TestMethod]
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
            var result = timeSeriesHelper.CreateHistogram();

            Dictionary<double, long> expected = new()
            {
               {10, 20+100},
               {20, 10+30},
               {600, 20},
            };

            CollectionAssert.AreEqual(expected.ToArray(), result.ToArray());
        }

        [TestMethod]
        public void NoValue()
        {
            List<TimeAndValue> dbValues = new();

            TimeSeriesHelper timeSeriesHelper = new(100, 199, dbValues);
            var result = timeSeriesHelper.CreateHistogram();

            Dictionary<double, long> expected = new();
            CollectionAssert.AreEqual(expected.ToArray(), result.ToArray());
        }

        [TestMethod]
        public void SingleValue()
        {
            List<TimeAndValue> dbValues = new()
            {
                new TimeAndValue(1, 10),
            };

            TimeSeriesHelper timeSeriesHelper = new(100, 199, dbValues);
            var result = timeSeriesHelper.CreateHistogram();

            Dictionary<double, long> expected = new()
            {
               {10, 100},
            };

            CollectionAssert.AreEqual(expected.ToArray(), result.ToArray());
        }
    }
}