using System;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;

#nullable enable

namespace Hspi
{
    internal sealed record HsFeatureData
    {
        private readonly IHsController homeSeerSystem;

        public HsFeatureData(IHsController homeSeerSystem, int deviceRef)
        {
            this.homeSeerSystem = homeSeerSystem;
            Ref = deviceRef;
        }

        public int Ref { get; }

        public double Value => Convert.ToDouble(GetPropertyValue<object>(EProperty.Value));
        public DateTimeOffset LastChange => GetPropertyValue<DateTime>(EProperty.LastChange);
        public string DisplayedStatus => GetPropertyValue<string>(EProperty.DisplayedStatus);

        private T GetPropertyValue<T>(EProperty prop)
        {
            return (T)homeSeerSystem.GetPropertyByRef(Ref, prop);
        }
    }
}