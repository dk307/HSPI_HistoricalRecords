﻿using System;
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

        public IImmutableDictionary<long, PerDeviceSettings> DeviceSettings => deviceSettings;

        public void AddOrUpdate(PerDeviceSettings device)
        {
            string id = device.DeviceRefId.ToString(CultureInfo.InvariantCulture);

            ImmutableInterlocked.AddOrUpdate(ref deviceSettings, device.DeviceRefId, (x) => device, (x, y) => device);

            SetValue(DeviceRefIdKey, device.DeviceRefId, id);
            SetValue(IsTrackedTag, device.IsTracked, id);
            SetValue(MinValueTag, device.MinValue?.ToString("g") ?? string.Empty, id);
            SetValue(MaxValueTag, device.MaxValue?.ToString("g") ?? string.Empty, id);
            SetValue(RetentionPeriodTag, device.RetentionPeriod?.ToString("c", CultureInfo.InvariantCulture) ?? string.Empty, id);
            SetValue(DeviceSettingsTag, deviceSettings.Keys.Aggregate((x, y) => x + DeviceSettingsIdsSeparator + y));
        }

        public void Remove(long deviceRefId)
        {
            ImmutableInterlocked.TryRemove(ref deviceSettings, deviceRefId, out var _);

            if (deviceSettings.Count > 0)
            {
                long value = deviceSettings.Keys.Aggregate((x, y) => x + DeviceSettingsIdsSeparator + y);
                SetValue(DeviceSettingsTag, value);
            }
            else
            {
                SetValue(DeviceSettingsTag, string.Empty);
            }

            string id = deviceRefId.ToString(CultureInfo.InvariantCulture);
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
                    T result = (T)Convert.ChangeType(stringValue, typeof(T), CultureInfo.InvariantCulture);
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
            var deviceSettingsIds = deviceIdsConcatString.Split(new char[] { DeviceSettingsIdsSeparator }, StringSplitOptions.RemoveEmptyEntries);

            var data = new Dictionary<long, PerDeviceSettings>();
            foreach (var id in deviceSettingsIds)
            {
                string deviceRefIdString = GetValue(DeviceRefIdKey, string.Empty, id);

                if (!long.TryParse(deviceRefIdString, out long deviceRefId))
                {
                    continue;
                }

                var isTracked = GetValue<bool>(IsTrackedTag, true, id);
                TimeSpan? retentionPeriod = ParseRetentionPeriod(id);

                var minValue = ParseDouble(id, MinValueTag);
                var maxValue = ParseDouble(id, MaxValueTag);

                data.Add(deviceRefId, new PerDeviceSettings(deviceRefId, isTracked, retentionPeriod,
                                                        minValue, maxValue));
            }

            this.deviceSettings = data.ToImmutableDictionary();

            TimeSpan? ParseRetentionPeriod(string id)
            {
                var retentionPeriodStr = GetValue<string>(RetentionPeriodTag, string.Empty, id);

                if (TimeSpan.TryParse(retentionPeriodStr, CultureInfo.InvariantCulture, out var temp))
                {
                    return temp;
                }

                return null;
            }

            double? ParseDouble(string id, string tag)
            {
                var retentionPeriodStr = GetValue<string>(tag, string.Empty, id);

                if (double.TryParse(retentionPeriodStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var temp))
                {
                    return temp;
                }

                return null;
            }
        }

        private void SetValue<T>(string key, T value, string section = DefaultSection)
        {
            string stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            HS.SaveINISetting(section, key, stringValue, fileName: PlugInData.SettingFileName);
        }

        private const string DefaultSection = "Settings";
        private const string DeviceRefIdKey = "RefId";
        private const char DeviceSettingsIdsSeparator = ',';
        private const string DeviceSettingsTag = "DeviceSettings";
        private const string IsTrackedTag = "IsTracked";
        private const string MaxValueTag = "MaxValue";
        private const string MinValueTag = "MinValue";
        private const string RetentionPeriodTag = "RetentionPeriod";
        private readonly IHsController HS;
        private ImmutableDictionary<long, PerDeviceSettings> deviceSettings = ImmutableDictionary<long, PerDeviceSettings>.Empty;
    }
}