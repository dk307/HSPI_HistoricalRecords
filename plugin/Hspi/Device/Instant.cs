using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

#nullable enable

namespace Hspi.Device
{
    internal sealed record Instant
    {
        public Instant(InstantType instantType, IDictionary<PeriodUnits, int> offsets)
        {
            Type = instantType;
            Offsets = offsets.ToImmutableSortedDictionary();
        }

        [JsonConstructor]
        public Instant(InstantType instantType, ImmutableSortedDictionary<PeriodUnits, int>? offsets = null)
        {
            Type = instantType;
            Offsets = offsets;
        }

        [JsonProperty(Required = Required.Always)]
        public InstantType Type { get; init; }

        [JsonProperty(Required = Required.Default)]
        public ImmutableSortedDictionary<PeriodUnits, int>? Offsets { get; init; }

        [JsonIgnore]
        public bool HasOffset => Offsets != null && Offsets.Count > 0;
    }
}