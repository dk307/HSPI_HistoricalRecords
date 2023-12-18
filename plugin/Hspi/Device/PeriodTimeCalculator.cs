#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Hspi.Device
{
    internal static class PeriodTimeCalculator
    {
        public static MinMaxValues CalculateMinMaxSeconds(this Period period, IGlobalClock timer)
        {
            var localNow = timer.LocalNow;
            var start = period.Start?.CalculateTime(localNow, timer.FirstDayOfWeek);
            var end = period.End?.CalculateTime(localNow, timer.FirstDayOfWeek);

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

            static long GetUnixTimeSeconds(DateTimeOffset localInstantTime)
            {
                return localInstantTime.ToUnixTimeSeconds();
            }

            static MinMaxValues CalculateFromStartAndEnd(DateTimeOffset start, DateTimeOffset end)
            {
                return new MinMaxValues(GetUnixTimeSeconds(start), GetUnixTimeSeconds(end));
            }
        }

        public static DateTimeOffset CalculateTime(this Instant instant, DateTimeOffset dt, DayOfWeek startOfWeek)
        {
            DateTimeOffset time = instant.Type switch
            {
                InstantType.Now => dt,
                InstantType.StartOfHour => StartOfHour(dt),
                InstantType.StartOfDay => StartOfDay(dt),
                InstantType.StartOfWeek => StartOfWeek(dt, startOfWeek),
                InstantType.StartOfMonth => StartOfMonth(dt),
                InstantType.StartOfYear => StartOfYear(dt),
                _ => throw new NotImplementedException(),
            };

            return instant.Offsets != null ? ApplyOffsets(time, instant.Offsets) : time;
        }

        private static DateTimeOffset ApplyOffsets(DateTimeOffset dt, ImmutableSortedDictionary<PeriodUnits, int> offsets)
        {
            foreach (var offset in offsets)
            {
                dt = offset.Key switch
                {
                    PeriodUnits.Years => dt.AddYears(offset.Value),
                    PeriodUnits.Months => dt.AddMonths(offset.Value),
                    PeriodUnits.Weeks => dt.AddDays(offset.Value * 7),
                    PeriodUnits.Days => dt.AddDays(offset.Value),
                    PeriodUnits.Hours => dt.AddHours(offset.Value),
                    PeriodUnits.Minutes => dt.AddMinutes(offset.Value),
                    PeriodUnits.Seconds => dt.AddSeconds(offset.Value),
                    _ => throw new NotImplementedException(),
                };
            }

            return dt;
        }

        private static DateTimeOffset StartOfDay(DateTimeOffset x) => new(x.Year, x.Month, x.Day, 0, 0, 0, x.Offset);

        private static DateTimeOffset StartOfHour(DateTimeOffset x) => new(x.Year, x.Month, x.Day, x.Hour, 0, 0, x.Offset);

        private static DateTimeOffset StartOfMonth(DateTimeOffset x) => new(x.Year, x.Month, 1, 0, 0, 0, x.Offset);

        private static DateTimeOffset StartOfWeek(this DateTimeOffset dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return StartOfDay(dt.AddDays(-diff));
        }

        private static DateTimeOffset StartOfYear(DateTimeOffset x) => new(x.Year, 1, 1, 0, 0, 0, x.Offset);
    }

    internal record struct MinMaxValues(long Minimum, long Maximum)
    {
        public readonly bool IsValid => Minimum <= Maximum;
    }
}