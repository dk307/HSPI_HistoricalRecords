using System;
using Newtonsoft.Json;

#nullable enable

namespace Hspi.Device
{
    internal sealed record class StatisticsFunctionDuration
    {
        public StatisticsFunctionDuration(PreDefinedPeriod preDefinedPeriod)
        {
            PreDefinedPeriod = preDefinedPeriod;
        }

        public StatisticsFunctionDuration(Period customPeriod)
        {
            CustomPeriod = customPeriod;
        }

        [JsonConstructor]
        public StatisticsFunctionDuration(Period? customPeriod, PreDefinedPeriod? preDefinedPeriod)
        {
            if ((customPeriod != null && preDefinedPeriod != null) ||
               (customPeriod == null && preDefinedPeriod == null))
            {
                throw new ArgumentException("Only one of customPeriod & preDefinedPeriod should be set");
            }

            CustomPeriod = customPeriod;
            PreDefinedPeriod = preDefinedPeriod;
        }

        public PreDefinedPeriod? PreDefinedPeriod { get; init; }
        public Period? CustomPeriod { get; init; }

        [JsonIgnore]
        public Period DerviedPeriod => CustomPeriod ??
                                     (PreDefinedPeriod != null ? Period.Create(PreDefinedPeriod.Value) : throw new ArgumentException("Invalid state"));
    }
}