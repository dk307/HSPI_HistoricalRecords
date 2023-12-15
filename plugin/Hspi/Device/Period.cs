using System;
using Newtonsoft.Json;

#nullable enable

namespace Hspi.Device
{
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
    }
}