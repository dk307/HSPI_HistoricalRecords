using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;

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

        public double Value => GetPropertyValue<double>(EProperty.Value);
        public DateTimeOffset LastChange => GetPropertyValue<DateTime>(EProperty.LastChange);
        public string DisplayedStatus => GetPropertyValue<string>(EProperty.DisplayedStatus);
        public List<StatusControl> StatusControls => GetPropertyValue<List<StatusControl>>(EProperty.StatusControls);
        public List<StatusGraphic> StatusGraphics => GetPropertyValue<List<StatusGraphic>>(EProperty.StatusGraphics);

        private T GetPropertyValue<T>(EProperty prop)
        {
            return (T)homeSeerSystem.GetPropertyByRef(Ref, prop);
        }
    }
}