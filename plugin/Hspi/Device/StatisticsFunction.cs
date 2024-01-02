#nullable enable

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace Hspi.Device
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum InstantType
    {
        Now = 0,
        StartOfHour,
        StartOfDay,
        StartOfWeek,
        StartOfMonth,
        StartOfYear,
    };

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PeriodUnits
    {
        Years = 0,
        Months,
        Weeks,
        Days,
        Hours,
        Minutes,
        Seconds,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StatisticsFunction
    {
        AverageStep = 0,
        AverageLinear = 1,
        MinimumValue = 2,
        MaximumValue = 3,
        DistanceBetweenMinAndMax = 4,
        RecordsCount = 5,
        ValueChangedCount = 6
    };
}