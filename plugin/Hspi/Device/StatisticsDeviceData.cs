using Newtonsoft.Json;

#nullable enable

namespace Hspi.Device
{
    internal sealed record StatisticsDeviceData(
                [property: JsonProperty(Required = Required.Always)] int TrackedRef,
                [property: JsonProperty(Required = Required.Always)] StatisticsFunction StatisticsFunction,
                [property: JsonProperty(Required = Required.Always)] Period Period,
                [property: JsonProperty(Required = Required.Always)] long RefreshIntervalSeconds);
}