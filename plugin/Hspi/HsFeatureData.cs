using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;

#nullable enable

namespace Hspi
{
    internal readonly struct HsFeatureData(IHsController homeSeerSystem, int deviceRef)
    {
        public string DisplayedStatus => GetPropertyValue<string>(EProperty.DisplayedStatus);
        public DateTimeOffset LastChange => GetPropertyValue<DateTime>(EProperty.LastChange);
        public int Ref { get; } = deviceRef;
        public List<StatusControl> StatusControls => GetPropertyValue<List<StatusControl>>(EProperty.StatusControls);
        public List<StatusGraphic> StatusGraphics => GetPropertyValue<List<StatusGraphic>>(EProperty.StatusGraphics);
        public double Value => GetPropertyValue<double>(EProperty.Value);

        private T GetPropertyValue<T>(EProperty prop) => (T)homeSeerSystem.GetPropertyByRef(Ref, prop);
    }
}