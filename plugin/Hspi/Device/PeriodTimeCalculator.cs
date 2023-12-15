#nullable enable

using System;
using System.Collections.Immutable;

namespace Hspi.Device
{
    internal static class PeriodTimeCalculator
    {
        public static MinMaxValues CalculateMinMaxSeconds(this Period period, IGlobalClock timer)
        {
            var utcNow = timer.UtcNow;
            var start = period.Start?.CalculateTime(utcNow, timer.TimeZone, timer.FirstDayOfWeek);
            var end = period.End?.CalculateTime(utcNow, timer.TimeZone, timer.FirstDayOfWeek);

            if (start == null && end != null && period.FunctionDurationSeconds != null)
            {
                start = end.Value.AddSeconds(-(double)period.FunctionDurationSeconds.Value);
                return CalculateFromStartAndEnd(start.Value, end.Value);
            }
            else if (start != null && end == null && period.FunctionDurationSeconds != null)
            {
                end = start.Value.AddSeconds(period.FunctionDurationSeconds.Value);
                return CalculateFromStartAndEnd(start.Value, end.Value);
            }
            else if (start != null && end != null && period.FunctionDurationSeconds == null)
            {
                return CalculateFromStartAndEnd(start.Value, end.Value);
            }
            else
            {
                throw new ArgumentException("2 of start, end & duration should be set", nameof(period));
            }

            static long GetUnixTimeSeconds(DateTime localInstantTime)
            {
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(localInstantTime);
                return new DateTimeOffset(utcTime).ToUnixTimeSeconds();
            }

            static MinMaxValues CalculateFromStartAndEnd(DateTime start, DateTime end)
            {
                return new MinMaxValues(GetUnixTimeSeconds(start), GetUnixTimeSeconds(end));
            }
        }

        public static DateTime CalculateTime(this Instant instant, DateTime utcNow, TimeZoneInfo timeZoneInfo, DayOfWeek startOfWeek)
        {
            var time = instant.Type switch
            {
                InstantType.Now => utcNow,
                InstantType.StartOfHour => Convert(utcNow, timeZoneInfo, x => StartOfHour(x)),
                InstantType.StartOfDay => Convert(utcNow, timeZoneInfo, x => StartOfDay(x)),
                InstantType.StartOfWeek => Convert(utcNow, timeZoneInfo, x => StartOfWeek(x, startOfWeek)),
                InstantType.StartOfMonth => Convert(utcNow, timeZoneInfo, x => StartOfMonth(x)),
                InstantType.StartOfYear => Convert(utcNow, timeZoneInfo, x => StartOfYear(x)),
                _ => throw new NotImplementedException(),
            };

            return instant.Offsets != null ? ApplyOffsets(time, instant.Offsets) : time;

            static DateTime Convert(DateTime utcNow,
                                    TimeZoneInfo timeZoneInfo,
                                    Func<DateTime, DateTime> convertor)
            {
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZoneInfo);
                return convertor(localTime);
            }
        }

        private static DateTime ApplyOffsets(DateTime x,
                                             ImmutableSortedDictionary<PeriodUnits, int> offsets)
        {
            foreach (var offset in offsets)
            {
                x = offset.Key switch
                {
                    PeriodUnits.Years => x.AddYears(offset.Value),
                    PeriodUnits.Months => x.AddMonths(offset.Value),
                    PeriodUnits.Weeks => x.AddDays(offset.Value * 7),
                    PeriodUnits.Days => x.AddDays(offset.Value),
                    PeriodUnits.Hours => x.AddHours(offset.Value),
                    PeriodUnits.Minutes => x.AddMinutes(offset.Value),
                    PeriodUnits.Seconds => x.AddSeconds(offset.Value),
                    _ => throw new NotImplementedException(),
                };
            }

            return x;
        }

        private static DateTime StartOfDay(DateTime x) => new(x.Year, x.Month, x.Day, 0, 0, 0, x.Kind);

        private static DateTime StartOfHour(DateTime x) => new(x.Year, x.Month, x.Day, x.Hour, 0, 0, x.Kind);

        private static DateTime StartOfMonth(DateTime x) => new(x.Year, x.Month, 1, 0, 0, 0, x.Kind);

        private static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-diff).Date;
        }

        private static DateTime StartOfYear(DateTime x) => new(x.Year, 1, 1, 0, 0, 0, x.Kind);
    }

    internal record struct MinMaxValues(long Minimum, long Maximum)
    {
        public readonly bool IsValid => Minimum <= Maximum;
    }
}