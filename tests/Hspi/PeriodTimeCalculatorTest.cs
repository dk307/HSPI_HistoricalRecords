using System;
using System.Collections.Generic;
using Hspi;
using Hspi.Device;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class PeriodTimeCalculatorTest

    {
        [TestCase(31536000UL)] // 1 year
        [TestCase(60UL)]
        public void CalculateMinMaxSecondsForLastXDuration(ulong count)
        {
            var period = new Period(null, new Instant(InstantType.Now), count);

            FakeGlobalClock fakeGlobalClock = new(dateTime1, TimeZoneInfo.FindSystemTimeZoneById("UTC"), DayOfWeek.Monday);

            Assert.That(period.CalculateMinMaxSeconds(fakeGlobalClock),
                                                      Is.EqualTo(new MinMaxValues(dateTime1.AddSeconds(-(double)count).ToUnixTimeSeconds(),
                                                                                  dateTime1.ToUnixTimeSeconds())));
        }

        [TestCaseSource(nameof(CreateInstantCalculateTimeCases))]
        public void InstantCalculateTime(InstantType type, IDictionary<PeriodUnits, int> offsets,
                                            string timezoneId, DateTime dateTime, int expectedOffset)
        {
            var instant = new Instant(type, offsets);
            Assert.That(instant.CalculateTime(dateTime, TimeZoneInfo.FindSystemTimeZoneById(timezoneId), DayOfWeek.Monday),
                        Is.EqualTo(dateTime.AddSeconds(expectedOffset)));
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
            yield return new object[] { InstantType.Now, EmptyOffsets, utcTimeZone, dateTimeStartOfDay, 0 };
            yield return new object[] { InstantType.Now, EmptyOffsets, "Tokyo Standard Time", dateTimeStartOfDay, 0 };

            // Try each type
            yield return new object[] { InstantType.Now, OffsetFromSeconds(10), utcTimeZone, dateTimeStartOfDay, 10 };
            yield return new object[] { InstantType.Now, OffsetFromMinutes(6), utcTimeZone, dateTimeStartOfDay, 6 * SecondsInMinute };
            yield return new object[] { InstantType.Now, OffsetFromHours(6), utcTimeZone, dateTimeStartOfDay, 6 * SecondsInHour };
            yield return new object[] { InstantType.Now, OffsetFromDays(-1), utcTimeZone, dateTimeStartOfDay, -SecondsInDay };
            yield return new object[] { InstantType.Now, OffsetFromMonths(1), utcTimeZone, dateTimeStartOfDay, SecondsInDay * 30 };
            yield return new object[] { InstantType.Now, OffsetFromYears(-1), utcTimeZone, dateTimeStartOfDay, -SecondsInDay * 365 };

            //combined
            yield return new object[] { InstantType.Now, new Dictionary<PeriodUnits, int>() {
                {PeriodUnits.Years, 1 },
                { PeriodUnits.Months, 1},
                { PeriodUnits.Days, 1 },
                { PeriodUnits.Hours, 1 },
                { PeriodUnits.Minutes, 1 },
                { PeriodUnits.Seconds, 1 },
            },

            utcTimeZone, dateTimeStartOfDay,
            SecondsInDay * 365 + SecondsInDay * 30 + SecondsInDay + SecondsInHour + SecondsInMinute + 1};

            // Leap year check
            yield return new object[] { InstantType.Now, OffsetFromYears(2), utcTimeZone, dateTimeStartOfDay, 2 * SecondsInDay * 365 + SecondsInDay };

            yield return new object[] { InstantType.Now,
                           new Dictionary<PeriodUnits, int>() { {PeriodUnits.Days, 14}, {PeriodUnits.Hours, 2 }, { PeriodUnits.Minutes, 1 } },
                           "Pacific Standard Time", dateTimeStartOfDay,
                           SecondsInDay * 14 + SecondsInHour * 2 + SecondsInMinute  };

            // start of hour
            yield return new object[] { InstantType.StartOfHour, EmptyOffsets, utcTimeZone, dateTime1,
                                       -(dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfHour, OffsetFromHours(1), utcTimeZone, dateTime1,
                                       SecondsInHour -(dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfHour, EmptyOffsets, utcTimeZone, dateTimeStartOfDay, 0 };

            // start of day
            yield return new object[] { InstantType.StartOfDay, EmptyOffsets, utcTimeZone, dateTime1,
                                       -(dateTime1.Hour * SecondsInHour +  dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfDay, OffsetFromHours(1), utcTimeZone, dateTime1,
                                       SecondsInHour -(dateTime1.Hour * SecondsInHour + dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfDay, OffsetFromDays(-1), utcTimeZone, dateTime1,
                                       -SecondsInDay -(dateTime1.Hour * SecondsInHour + dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfDay, EmptyOffsets, utcTimeZone, dateTimeStartOfDay, 0 };

            // start of month
            yield return new object[] { InstantType.StartOfMonth, EmptyOffsets, utcTimeZone, dateTime1,
                                       -((dateTime1.Day - 1) * SecondsInDay + dateTime1.Hour * SecondsInHour +  dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfMonth, OffsetFromMonths(-1), utcTimeZone, dateTime1,
                                       -SecondsInDay * 31 -((dateTime1.Day -1) * SecondsInDay + dateTime1.Hour * SecondsInHour + dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfMonth, EmptyOffsets, utcTimeZone, dateTimeStartOfDay, 0 };

            // start of year
            yield return new object[] { InstantType.StartOfYear, EmptyOffsets, utcTimeZone, dateTime1,
                                       -(162 * SecondsInDay + dateTime1.Hour * SecondsInHour +  dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
            yield return new object[] { InstantType.StartOfYear, OffsetFromMonths(1), utcTimeZone, dateTime1,
                                       -((162 -31) * SecondsInDay + dateTime1.Hour * SecondsInHour +  dateTime1.Minute * SecondsInMinute + dateTime1.Second) };

            // start of week
            yield return new object[] { InstantType.StartOfWeek, EmptyOffsets, utcTimeZone, dateTime1,
                                       -((1) * SecondsInDay + dateTime1.Hour * SecondsInHour +  dateTime1.Minute * SecondsInMinute + dateTime1.Second) };
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
        private const string utcTimeZone = "UTC";
        private static readonly DateTime dateTime1 = new(2024, 6, 11, 11, 23, 30);
        private static readonly DateTime dateTimeStartOfDay = new(2022, 11, 1, 0, 0, 0);
        private static readonly Dictionary<PeriodUnits, int> EmptyOffsets = new();
        private record FakeGlobalClock(DateTime UtcNow, TimeZoneInfo TimeZone, DayOfWeek FirstDayOfWeek) : IGlobalClock;
    }
}