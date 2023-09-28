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
    internal sealed class MonitoredDevicesConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoredDevicesConfig"/> class.
        /// </summary>
        /// <param name="HS">The homeseer application.</param>
        public MonitoredDevicesConfig(IHsController HS)
        {
            this.HS = HS;
            LoadPersistenceSettings();
        }

        public IImmutableDictionary<int, MonitoredDevice> MonitoredDevices => monitoredDevices;

        public void AddDevicePersistenceData(MonitoredDevice device)
        {
            string id = device.DeviceRefId.ToString(CultureInfo.InvariantCulture);

            ImmutableDictionary<int, MonitoredDevice>.Builder builder = monitoredDevices.ToBuilder();
            builder.Add(device.DeviceRefId, device);
            monitoredDevices = builder.ToImmutableDictionary();

            SetValue(DeviceRefIdKey, device.DeviceRefId, id);
            SetValue(MaxValidValueKey, device.MaxValidValue, id);
            SetValue(MinValidValueKey, device.MinValidValue, id);
            SetValue(TrackedTypeKey, device.TrackedType, id);
            SetValue(PersistenceIdsKey, monitoredDevices.Keys.Aggregate((x, y) => x + PersistenceIdsSeparator + y));
        }

        public void RemoveDevicePersistenceData(int deviceRef)
        {
            string id = deviceRef.ToString(CultureInfo.InvariantCulture);

            ImmutableDictionary<int, MonitoredDevice>.Builder builder = monitoredDevices.ToBuilder();
            builder.Remove(deviceRef);
            monitoredDevices = builder.ToImmutableDictionary();

            if (monitoredDevices.Count > 0)
            {
                SetValue(PersistenceIdsKey, monitoredDevices.Keys.Aggregate((x, y) => x + PersistenceIdsSeparator + y));
            }
            else
            {
                SetValue(PersistenceIdsKey, string.Empty);
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

        private void LoadPersistenceSettings()
        {
            string deviceIdsConcatString = GetValue(PersistenceIdsKey, string.Empty);
            var persistenceIds = deviceIdsConcatString.Split(PersistenceIdsSeparator);

            var data = new Dictionary<int, MonitoredDevice>();
            foreach (var persistenceId in persistenceIds)
            {
                string deviceRefIdString = GetValue(DeviceRefIdKey, string.Empty, persistenceId);

                if (!int.TryParse(deviceRefIdString, out int deviceRefId))
                {
                    continue;
                }

                string maxValidValueString = GetValue(MaxValidValueKey, string.Empty, persistenceId);
                string minValidValueString = GetValue(MinValidValueKey, string.Empty, persistenceId);
                string trackedTypeString = GetValue(TrackedTypeKey, string.Empty, persistenceId);

                double? maxValidValue = null;
                double? minValidValue = null;
                TrackedType? trackedType = null;

                if (double.TryParse(maxValidValueString, out var value))
                {
                    maxValidValue = value;
                }

                if (double.TryParse(minValidValueString, out value))
                {
                    minValidValue = value;
                }

                if (Enum.TryParse<TrackedType>(trackedTypeString, out var trackedTypeValue))
                {
                    trackedType = trackedTypeValue;
                }

                this.monitoredDevices.Add(deviceRefId, new MonitoredDevice(deviceRefId, maxValidValue, minValidValue, trackedType));
            }

            this.monitoredDevices = data.ToImmutableDictionary();
        }

        private void SetValue<T>(string key, T value, string section = DefaultSection)
        {
            string stringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
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
        private const string DefaultFieldValueString = "value";
        private const string DefaultSection = "Settings";
        private const string DeviceNameTag = "name";
        private const string DeviceRefIdKey = "DeviceRefId";
        private const string DeviceRefIdTag = "refid";
        private const string MaxValidValueKey = "MaxValidValue";
        private const string MinValidValueKey = "MinValidValue";
        private const string PersistenceIdsKey = "PersistenceIds";
        private const char PersistenceIdsSeparator = ',';
        private const string TrackedTypeKey = "TrackedType";
        private readonly IHsController HS;
        private ImmutableDictionary<int, MonitoredDevice> monitoredDevices = ImmutableDictionary<int, MonitoredDevice>.Empty;
    }
}