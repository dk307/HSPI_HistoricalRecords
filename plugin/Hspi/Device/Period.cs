using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#nullable enable

namespace Hspi.Device
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PreDefinedPeriod
    {
        ThisHour,
        Today,
        Yesterday,
        ThisWeek,
        ThisMonth,
    };

    internal sealed record Period
    {
        [JsonConstructor]
        public Period(Instant? start, Instant? end, ulong? functionDurationSeconds)
        {
            Start = start;
            End = end;
            FunctionDurationSeconds = functionDurationSeconds;

            if ((Start != null || End == null || FunctionDurationSeconds == null) &&
                (Start == null || End != null || FunctionDurationSeconds == null) &&
                (Start == null || End == null || FunctionDurationSeconds != null))
            {
                throw new ArgumentException("2 of start, end & duration should be set");
            }
        }

        [JsonProperty(Required = Required.Default)]
        public Instant? Start { get; init; }

        [JsonProperty(Required = Required.Default)]
        public Instant? End { get; init; }

        [JsonProperty(Required = Required.Default)]
        public ulong? FunctionDurationSeconds { get; init; }

        public static Period Create(PreDefinedPeriod preDefinedPeriod)
        {
            return preDefinedPeriod switch
            {
                PreDefinedPeriod.ThisHour => new Period(new Instant(InstantType.StartOfHour), new Instant(InstantType.Now), null),
                PreDefinedPeriod.Today => new Period(new Instant(InstantType.StartOfDay), new Instant(InstantType.Now), null),
                PreDefinedPeriod.Yesterday => new Period(new Instant(InstantType.StartOfDay, new Dictionary<PeriodUnits, int> { { PeriodUnits.Days, -1 } }), new Instant(InstantType.StartOfDay), null),
                PreDefinedPeriod.ThisWeek => new Period(new Instant(InstantType.StartOfWeek), new Instant(InstantType.Now), null),
                PreDefinedPeriod.ThisMonth => new Period(new Instant(InstantType.StartOfMonth), new Instant(InstantType.Now), null),
                _ => throw new NotImplementedException(),
            };
        }

        public static Period CreatePastInterval(ulong seconds)
        {
            return new Period(null, new Instant(InstantType.Now), seconds);
        }
    }
}