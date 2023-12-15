#nullable enable

using System;
using System.Collections.Generic;

namespace Hspi.Device
{
    internal static class PeriodHumanizer
    {
        public static string Humanize(this Period period)
        {
            if (period.End != null && !period.End.HasOffset && period.FunctionDurationSeconds != null)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(period.FunctionDurationSeconds.Value);
                return "Last - " + HumanizeTimeSpan(timeSpan);
            }

            return "TODO";
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
    }
}