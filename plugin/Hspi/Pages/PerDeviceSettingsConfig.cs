using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using HomeSeer.PluginSdk;

#nullable enable

namespace Hspi
{
    /// <summary>
    /// Class to store Monitored Devices Configuration
    /// </summary>
    internal sealed class PerDeviceSettingsConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PerDeviceSettingsConfig"/> class.
        /// </summary>
        /// <param name="HS">The homeseer application.</param>
        public PerDeviceSettingsConfig(IHsController HS)
        {
            this.HS = HS;
            LoadSettings();
        }

        public IImmutableDictionary<long, PerDeviceSettings> MonitoredDevices => monitoredDevices;

        public void AddOrUpdate(PerDeviceSettings device)
        {
            string id = device.DeviceRefId.ToString(CultureInfo.InvariantCulture);

            ImmutableDictionary<long, PerDeviceSettings>.Builder builder = monitoredDevices.ToBuilder();
            builder.Remove(device.DeviceRefId);
            builder.Add(device.DeviceRefId, device);
            monitoredDevices = builder.ToImmutableDictionary();

            SetValue(DeviceRefIdKey, device.DeviceRefId, id);
            SetValue(IsTrackedTag, device.IsTracked, id);
            SetValue(RetentionPeriodTag, device.RetentionPeriod.TotalSeconds, id);
            SetValue(DeviceSettingsTag, monitoredDevices.Keys.Aggregate((x, y) => x + DeviceSettingsIdsSeparator + y));
        }

        public void Remove(long deviceRefId)
        {
            string id = deviceRefId.ToString(CultureInfo.InvariantCulture);

            ImmutableDictionary<long, PerDeviceSettings>.Builder builder = monitoredDevices.ToBuilder();
            builder.Remove(deviceRefId);
            monitoredDevices = builder.ToImmutableDictionary();

            if (monitoredDevices.Count > 0)
            {
                SetValue(DeviceSettingsTag, monitoredDevices.Keys.Aggregate((x, y) => x + DeviceSettingsIdsSeparator + y));
            }
            else
            {
                SetValue(DeviceSettingsTag, string.Empty);
            }

            ClearSection(id);
        }

        private void ClearSection(string id)
        {
            HS.ClearIniSection(id, PlugInData.SettingFileName);
        }

        private T GetValue<T>(string key, T defaultValue)
        {
            return GetValue(key, defaultValue, DefaultSection);
        }

        private T GetValue<T>(string key, T defaultValue, string section)
        {
            string stringValue = HS.GetINISetting(section, key, null, fileName: PlugInData.SettingFileName);

            if (stringValue != null)
            {
                try
                {
                    T result = (T)System.Convert.ChangeType(stringValue, typeof(T), CultureInfo.InvariantCulture);
                    return result;
                }
                catch (Exception)
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private void LoadSettings()
        {
            string deviceIdsConcatString = GetValue(DeviceSettingsTag, string.Empty);
            var deviceSettingsIds = deviceIdsConcatString.Split(new char []{ DeviceSettingsIdsSeparator}, StringSplitOptions.RemoveEmptyEntries);

            var data = new Dictionary<long, PerDeviceSettings>();
            foreach (var id in deviceSettingsIds)
            {
                string deviceRefIdString = GetValue(DeviceRefIdKey, string.Empty, id);

                if (!long.TryParse(deviceRefIdString, out long deviceRefId))
                {
                    continue;
                }

                var isTracked = GetValue<bool>(IsTrackedTag, true, id);
                var retentionPeriodSeconds = GetValue<long>(RetentionPeriodTag, 0, id);

                this.monitoredDevices.Add(deviceRefId,
                                          new PerDeviceSettings(deviceRefId, isTracked, TimeSpan.FromSeconds(retentionPeriodSeconds)));
            }

            this.monitoredDevices = data.ToImmutableDictionary();
        }

        private void SetValue<T>(string key, T value, string section = DefaultSection)
        {
            string stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            HS.SaveINISetting(section, key, stringValue, fileName: PlugInData.SettingFileName);
        }

        private void SetValue<T>(string key, Nullable<T> value, string section = DefaultSection) where T : struct
        {
            string stringValue = value.HasValue ? System.Convert.ToString(value.Value, CultureInfo.InvariantCulture) : string.Empty;
            HS.SaveINISetting(section, key, stringValue, fileName: PlugInData.SettingFileName);
        }

        private void SetValue<T>(string key, T value, ref T oldValue)
        {
            SetValue<T>(key, value, ref oldValue, DefaultSection);
        }

        private void SetValue<T>(string key, T value, ref T oldValue, string section)
        {
            if (!object.Equals(value, oldValue))
            {
                string stringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
                HS.SaveINISetting(section, key, stringValue, fileName: PlugInData.SettingFileName);
                oldValue = value;
            }
        }

        private const string DefaultSection = "Settings";
        private const string DeviceRefIdKey = "DeviceRefId";
        private const string IsTrackedTag = "IsTracked";
        private const string RetentionPeriodTag = "RetentionPeriod";
        private const string DeviceSettingsTag = "DeviceSettings";
        private const char DeviceSettingsIdsSeparator = ',';
        private readonly IHsController HS;
        private ImmutableDictionary<long, PerDeviceSettings> monitoredDevices = ImmutableDictionary<long, PerDeviceSettings>.Empty;
    }
}