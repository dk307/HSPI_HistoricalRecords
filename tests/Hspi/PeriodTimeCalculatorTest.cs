using System;
using System.Collections.Generic;
using Hspi;
using Hspi.Device;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class PeriodTimeCalculatorTest

    {
        [Test]
        public void CalculateMinMaxSecondsForPredefinedPeriod([Values] PreDefinedPeriod preDefinedPeriod)
        {
            var period = Period.Create(preDefinedPeriod);

            var dt = dateTimeLocal1;

            FakeGlobalClock fakeGlobalClock = new(DateTimeOffset.MinValue, dateTimeLocal1, DayOfWeek.Monday);

            DateTimeOffset minDateTime;
            DateTimeOffset maxDateTime;

            switch (preDefinedPeriod)
            {
                case PreDefinedPeriod.ThisHour:
                    minDateTime = new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Offset);
                    maxDateTime = dt;
                    break;

                case PreDefinedPeriod.Today:
                    minDateTime = new DateTimeOffset(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Offset);
                    maxDateTime = dt;
                    break;

                case PreDefinedPeriod.Yesterday:
                    maxDateTime = new DateTimeOffset(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Offset);
                    minDateTime = maxDateTime.AddDays(-1);
                    break;

                case PreDefinedPeriod.ThisWeek:
                    minDateTime = (new DateTimeOffset(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Offset)).AddDays(-1);
                    maxDateTime = dt;

                    break;

                case PreDefinedPeriod.ThisMonth:
                    minDateTime = new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset);
                    maxDateTime = dt;
                    break;

                default:
                    throw new NotImplementedException();
            }

            Assert.That(period.CalculateMinMaxSeconds(fakeGlobalClock),
                                                      Is.EqualTo(new MinMaxValues(minDateTime.ToUnixTimeSeconds(),
                                                                                  maxDateTime.ToUnixTimeSeconds())));
        }

        [TestCase(31536000UL)] // 1 year
        [TestCase(60UL)]
        public void CalculateMinMaxSecondsForPastDuration(ulong count)
        {
            var period = new Period(null, new Instant(InstantType.Now), count);

            FakeGlobalClock fakeGlobalClock = new(DateTimeOffset.MinValue, dateTimeLocal1, DayOfWeek.Monday);

            Assert.That(period.CalculateMinMaxSeconds(fakeGlobalClock),
                                                      Is.EqualTo(new MinMaxValues(dateTimeLocal1.AddSeconds(-(double)count).ToUnixTimeSeconds(),
                                                                                  dateTimeLocal1.ToUnixTimeSeconds())));
        }

        [Test]
        public void InstantCalculateTimeForSundayStartOfWeek()
        {
            var instant = new Instant(InstantType.StartOfWeek, null);
            Assert.That(instant.CalculateTime(dateTimeLocal1, DayOfWeek.Sunday),
                        Is.EqualTo(dateTimeLocal1.AddSeconds(-2 * SecondsInDay - (dateTimeLocal1.Hour * SecondsInHour + dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second))));
        }

        [TestCaseSource(nameof(CreateInstantCalculateTimeCases))]
        public void InstantCalculateTime(InstantType type, IDictionary<PeriodUnits, int> offsets,
                                         DateTimeOffset dateTime, int expectedOffset)
        {
            var instant = new Instant(type, offsets);
            Assert.That(instant.CalculateTime(dateTime, DayOfWeek.Monday).ToUnixTimeSeconds(),
                        Is.EqualTo(dateTime.AddSeconds(expectedOffset).ToUnixTimeSeconds()));
        }

        [Test]
        public void InvalidPeriod()
        {
            Assert.Throws<ArgumentException>(() => new Period(null, null, null));
            Assert.Throws<ArgumentException>(() => new Period(new Instant(InstantType.Now), null, null));
            Assert.Throws<ArgumentException>(() => new Period(null, new Instant(InstantType.Now), null));
            Assert.Throws<ArgumentException>(() => new Period(null, null, 1000UL));
            Assert.Throws<ArgumentException>(() => new Period(new Instant(InstantType.Now), new Instant(InstantType.Now), 1000UL));
        }

        private static IEnumerable<object[]> CreateInstantCalculateTimeCases()
        {
            yield return new object[] { InstantType.Now, EmptyOffsets, dateTimeStartOfDayUtc, 0 };
            yield return new object[] { InstantType.Now, EmptyOffsets, dateTimeStartOfDayUtc, 0 };

            // Try each type
            yield return new object[] { InstantType.Now, OffsetFromSeconds(10), dateTimeStartOfDayUtc, 10 };
            yield return new object[] { InstantType.Now, OffsetFromMinutes(6), dateTimeStartOfDayUtc, 6 * SecondsInMinute };
            yield return new object[] { InstantType.Now, OffsetFromHours(6), dateTimeStartOfDayUtc, 6 * SecondsInHour };
            yield return new object[] { InstantType.Now, OffsetFromDays(-1), dateTimeStartOfDayUtc, -SecondsInDay };
            yield return new object[] { InstantType.Now, OffsetFromMonths(1), dateTimeStartOfDayUtc, SecondsInDay * 30 };
            yield return new object[] { InstantType.Now, OffsetFromYears(-1), dateTimeStartOfDayUtc, -SecondsInDay * 365 };

            //combined
            yield return new object[] { InstantType.Now, new Dictionary<PeriodUnits, int>() {
                {PeriodUnits.Years, 1 },
                { PeriodUnits.Months, 1},
                { PeriodUnits.Days, 1 },
                { PeriodUnits.Hours, 1 },
                { PeriodUnits.Minutes, 1 },
                { PeriodUnits.Seconds, 1 },
            },

             dateTimeStartOfDayUtc,
            SecondsInDay * 365 + SecondsInDay * 30 + SecondsInDay + SecondsInHour + SecondsInMinute + 1};

            // Leap year check
            yield return new object[] { InstantType.Now, OffsetFromYears(2), dateTimeStartOfDayUtc, 2 * SecondsInDay * 365 + SecondsInDay };

            yield return new object[] { InstantType.Now,
                           new Dictionary<PeriodUnits, int>() { {PeriodUnits.Days, 14}, {PeriodUnits.Hours, 2 }, { PeriodUnits.Minutes, 1 } },
                           dateTimeStartOfDayUtc,
                           SecondsInDay * 14 + SecondsInHour * 2 + SecondsInMinute  };

            // start of hour
            yield return new object[] { InstantType.StartOfHour, EmptyOffsets,  dateTimeLocal1,
                                       -(dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfHour, OffsetFromHours(1),  dateTimeLocal1,
                                       SecondsInHour -(dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfHour, EmptyOffsets, dateTimeStartOfDayUtc, 0 };

            // start of day
            yield return new object[] { InstantType.StartOfDay, EmptyOffsets,  dateTimeLocal1,
                                       -(dateTimeLocal1.Hour * SecondsInHour +  dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfDay, OffsetFromHours(1),  dateTimeLocal1,
                                       SecondsInHour -(dateTimeLocal1.Hour * SecondsInHour + dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfDay, OffsetFromDays(-1),  dateTimeLocal1,
                                       -SecondsInDay -(dateTimeLocal1.Hour * SecondsInHour + dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfDay, EmptyOffsets, dateTimeStartOfDayUtc, 0 };

            // start of month
            yield return new object[] { InstantType.StartOfMonth, EmptyOffsets,  dateTimeLocal1,
                                       -((dateTimeLocal1.Day - 1) * SecondsInDay + dateTimeLocal1.Hour * SecondsInHour +  dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfMonth, OffsetFromMonths(-1),  dateTimeLocal1,
                                       -SecondsInDay * 31 -((dateTimeLocal1.Day -1) * SecondsInDay + dateTimeLocal1.Hour * SecondsInHour + dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfMonth, EmptyOffsets, dateTimeStartOfDayUtc, 0 };

            // start of year
            yield return new object[] { InstantType.StartOfYear, EmptyOffsets,  dateTimeLocal1,
                                       -(162 * SecondsInDay + dateTimeLocal1.Hour * SecondsInHour +  dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
            yield return new object[] { InstantType.StartOfYear, OffsetFromMonths(1),  dateTimeLocal1,
                                       -((162 -31) * SecondsInDay + dateTimeLocal1.Hour * SecondsInHour +  dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };

            // start of week
            yield return new object[] { InstantType.StartOfWeek, EmptyOffsets,  dateTimeLocal1,
                                       -((1) * SecondsInDay + dateTimeLocal1.Hour * SecondsInHour +  dateTimeLocal1.Minute * SecondsInMinute + dateTimeLocal1.Second) };
        }

        private static Dictionary<PeriodUnits, int> OffsetFromDays(int count) => new() { { PeriodUnits.Days, count } };

        private static Dictionary<PeriodUnits, int> OffsetFromHours(int count) => new() { { PeriodUnits.Hours, count } };

        private static Dictionary<PeriodUnits, int> OffsetFromMinutes(int count) => new() { { PeriodUnits.Minutes, count } };

        private static Dictionary<PeriodUnits, int> OffsetFromMonths(int count) => new() { { PeriodUnits.Months, count } };

        private static Dictionary<PeriodUnits, int> OffsetFromSeconds(int count) => new() { { PeriodUnits.Seconds, count } };

        private static Dictionary<PeriodUnits, int> OffsetFromYears(int count) => new() { { PeriodUnits.Years, count } };

        private const int SecondsInDay = SecondsInHour * 24;
        private const int SecondsInHour = 60 * SecondsInMinute;
        private const int SecondsInMinute = 60;
        private static readonly DateTimeOffset dateTimeLocal1 = new(2024, 6, 11, 11, 23, 30, TimeSpan.FromHours(5));
        private static readonly DateTimeOffset dateTimeStartOfDayUtc = new(2022, 11, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly Dictionary<PeriodUnits, int> EmptyOffsets = new();
        private record FakeGlobalClock(DateTimeOffset UtcNow, DateTimeOffset LocalNow, DayOfWeek FirstDayOfWeek) : IGlobalClock;
    }
}