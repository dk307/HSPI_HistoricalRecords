using System.Collections.Generic;
using Hspi.Database;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class TimeAndValueIteratorTests
    {
        [Test]
        public void EmptyList()
        {
            var iterator = new TimeAndValueIterator(new List<TimeAndValue>(), 100000);

            Assert.That(!iterator.IsCurrentValid);
            Assert.That(!iterator.IsNextValid);

            iterator.MoveNext();

            Assert.That(!iterator.IsCurrentValid);
            Assert.That(!iterator.IsNextValid);
        }

        [Test]
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
                Assert.That(iterator.IsCurrentValid);
                Assert.That(iterator.IsNextValid);
                Assert.That(list[i], Is.EqualTo(iterator.Current));
                Assert.That(list[i + 1], Is.EqualTo(iterator.Next));
                Assert.That(iterator.FinishTimeForCurrentTimePoint, Is.EqualTo(i + 1));

                iterator.MoveNext();
            }

            Assert.That(iterator.IsCurrentValid);
            Assert.That(!iterator.IsNextValid);

            Assert.That(list[99], Is.EqualTo(iterator.Current));
            Assert.That(iterator.FinishTimeForCurrentTimePoint, Is.EqualTo(10000));

            iterator.MoveNext();

            Assert.That(!iterator.IsCurrentValid);
            Assert.That(!iterator.IsNextValid);
        }

        [Test]
        public void OneElementList()
        {
            List<TimeAndValue> list = new() { new TimeAndValue(1, 10) };
            var iterator = new TimeAndValueIterator(list, 10000);

            Assert.That(iterator.IsCurrentValid);
            Assert.That(!iterator.IsNextValid);
            Assert.That(list[0], Is.EqualTo(iterator.Current));
            Assert.That(iterator.FinishTimeForCurrentTimePoint, Is.EqualTo(10000));

            iterator.MoveNext();

            Assert.That(!iterator.IsCurrentValid);
            Assert.That(!iterator.IsNextValid);
        }
    }
}