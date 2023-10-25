using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class TimeAndValueIteratorTests
    {
        [TestMethod]
        public void EmptyList()
        {
            var iterator = new TimeAndValueIterator(new List<TimeAndValue>(), 100000);

            Assert.IsFalse(iterator.IsCurrentValid);
            Assert.IsFalse(iterator.IsNextValid);

            iterator.MoveNext();

            Assert.IsFalse(iterator.IsCurrentValid);
            Assert.IsFalse(iterator.IsNextValid);
        }

        [TestMethod]
        public void LongList()
        {
            List<TimeAndValue> list = new();

            for (int i = 0; i < 100; i++)
            {
                list.Add(new TimeAndValue(i, 10 + i));
            }

            var iterator = new TimeAndValueIterator(list, 10000);

            for (int i = 0; i < 99; i++)
            {
                Assert.IsTrue(iterator.IsCurrentValid);
                Assert.IsTrue(iterator.IsNextValid);
                Assert.AreEqual(iterator.Current, list[i]);
                Assert.AreEqual(iterator.Next, list[i + 1]);
                Assert.AreEqual(i + 1, iterator.FinishTimeForCurrentTimePoint);

                iterator.MoveNext();
            }

            Assert.IsTrue(iterator.IsCurrentValid);
            Assert.IsFalse(iterator.IsNextValid);

            Assert.AreEqual(iterator.Current, list[99]);
            Assert.AreEqual(10000, iterator.FinishTimeForCurrentTimePoint);

            iterator.MoveNext();

            Assert.IsFalse(iterator.IsCurrentValid);
            Assert.IsFalse(iterator.IsNextValid);
        }

        [TestMethod]
        public void OneElementList()
        {
            List<TimeAndValue> list = new() { new TimeAndValue(1, 10) };
            var iterator = new TimeAndValueIterator(list, 10000);

            Assert.IsTrue(iterator.IsCurrentValid);
            Assert.IsFalse(iterator.IsNextValid);
            Assert.AreEqual(iterator.Current, list[0]);
            Assert.AreEqual(10000, iterator.FinishTimeForCurrentTimePoint);

            iterator.MoveNext();

            Assert.IsFalse(iterator.IsCurrentValid);
            Assert.IsFalse(iterator.IsNextValid);
        }
    }
}