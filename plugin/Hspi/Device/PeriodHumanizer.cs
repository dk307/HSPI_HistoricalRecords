#nullable enable

using System;
using System.Collections.Generic;

namespace Hspi.Device
{
    internal static class PeriodHumanizer
    {
        public static string? Humanize(this StatisticsFunctionDuration duration)
        {
            if (duration.CustomPeriod is not null)
            {
                return duration.CustomPeriod.Humanize();
            }

            if (duration.PreDefinedPeriod is not null)
            {
                switch (duration.PreDefinedPeriod.Value)
                {
                    case PreDefinedPeriod.ThisHour: return ThisHour;
                    case PreDefinedPeriod.Today: return Today;
                    case PreDefinedPeriod.Yesterday: return Yesterday;
                    case PreDefinedPeriod.ThisWeek: return ThisWeek;
                    case PreDefinedPeriod.ThisMonth: return ThisMonth;
                }
            }

            return null;
        }

        public static string? Humanize(this Period period)
        {
            // Past fixed custom duration to present
            if (period.End != null && !period.End.HasOffset && period.FunctionDurationSeconds != null)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(period.FunctionDurationSeconds.Value);
                return "Last - " + HumanizeTimeSpan(timeSpan);
            }

            // Start of current hour to present
            if (period.Start?.Type == InstantType.StartOfHour && period.End?.Type == InstantType.Now)
            {
                return ThisHour;
            }

            // Start of current day to present
            if (period.Start?.Type == InstantType.StartOfDay && period.End?.Type == InstantType.Now)
            {
                return Today;
            }

            // Start of current week to present
            if (period.Start?.Type == InstantType.StartOfWeek && period.End?.Type == InstantType.Now)
            {
                return ThisWeek;
            }

            // Start of current month to present
            if (period.Start?.Type == InstantType.StartOfMonth && period.End?.Type == InstantType.Now)
            {
                return ThisMonth;
            }

            // Start of current year to present
            if (period.Start?.Type == InstantType.StartOfMonth && period.End?.Type == InstantType.Now)
            {
                return ThisYear;
            }

            return null;
        }

        private static string HumanizeTimeSpan(TimeSpan timeSpan)
        {
            List<string> parts = new();

            AddPart(timeSpan.Days, "day", parts);
            AddPart(timeSpan.Hours, "hour", parts);
            AddPart(timeSpan.Minutes, "minute", parts);
            AddPart(timeSpan.Seconds, "second", parts);

            return string.Join(" ", parts);
            static string Plural(int value)
            {
                return value > 1 ? "s" : string.Empty;
            }

            static void AddPart(int part, string partName, List<string> parts)
            {
                if (part > 0)
                {
                    parts.Add($"{part} {partName}{Plural(part)}");
                }
            }
        }

        private const string ThisHour = "This hour";
        private const string ThisMonth = "This month";
        private const string ThisWeek = "This week";
        private const string ThisYear = "This year";
        private const string Today = "Today";
        private const string Yesterday = "Yesterday";
    }
}