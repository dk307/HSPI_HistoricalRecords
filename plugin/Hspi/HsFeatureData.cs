using System;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;

#nullable enable

namespace Hspi
{
    internal sealed record HsFeatureData
    {
        private readonly IHsController homeSeerSystem;

        public HsFeatureData(IHsController homeSeerSystem, int deviceRef)
        {
            this.homeSeerSystem = homeSeerSystem;
            DeviceRef = deviceRef;
        }

        public int DeviceRef { get; }
        public string? Interface => GetPropertyValue<string>(EProperty.Interface);

        public TypeInfo TypeInfo => GetPropertyValue<TypeInfo>(EProperty.DeviceType);

        public double Value => GetPropertyValue<double>(EProperty.Value);
        public DateTimeOffset LastChange => GetPropertyValue<DateTime>(EProperty.LastChange);
        public string DisplayedStatus => GetPropertyValue<string>(EProperty.DisplayedStatus);

        private T GetPropertyValue<T>(EProperty prop)
        {
            return (T)homeSeerSystem.GetPropertyByRef(DeviceRef, prop);
        }
    }
}