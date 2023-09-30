using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Database;
using Hspi.Utils;
using Serilog;
using Constants = HomeSeer.PluginSdk.Constants;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInId, PlugInData.PlugInName)
        {
        }

        private enum ChangeType
        {
            Value,
            String
        };

        public override bool SupportsConfigDeviceAll => true;
        public override bool SupportsConfigFeature => true;

        public override string GetJuiDeviceConfigPage(int deviceRef)
        {
            var device = HomeSeerSystem.GetDeviceByRef(deviceRef);
            return CreateDeviceConfigPage(device, device.Interface == Id ? "editimport.html" : "devicehistoricalrecords.html");
        }

#pragma warning disable CS0618 // Type or member is obsolete

        public override void HsEvent(Constants.HSEvent eventType, object[] parameters)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            HSEventImpl(eventType, parameters).Wait(ShutdownCancellationToken);
        }

        protected override void BeforeReturnStatus()
        {
            this.Status = PluginStatus.Ok();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // deviceManager?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void Initialize()
        {
            try
            {
                Log.Information("Plugin Starting");
                Settings.Add(SettingsPages.CreateDefault());
                LoadSettingsFromIni();
                settingsPages = new SettingsPages(Settings);
                monitoredDevicesConfig = new MonitoredDevicesConfig(HomeSeerSystem);
                UpdateDebugLevel();

                string dbPath = Path.Combine(Path.GetTempPath(), "test.db");
                collector = new SqliteDatabaseCollector(dbPath, ShutdownCancellationToken);

#pragma warning disable CS0618 // Type or member is obsolete
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId);
#pragma warning restore CS0618 // Type or member is obsolete

                RestartProcessing();

                Log.Information("Plugin Started");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize PlugIn with {error}", ex.GetFullMessage());
                throw;
            }
        }

        protected override bool OnSettingChange(string pageId, AbstractView currentView, AbstractView changedView)
        {
            Log.Information("Page:{pageId} has changed value of id:{id} to {value}", pageId, changedView.Id, changedView.GetStringValue());

            CheckNotNull(settingsPages);

            if (settingsPages.OnSettingChange(changedView))
            {
                UpdateDebugLevel();
                return true;
            }

            return base.OnSettingChange(pageId, currentView, changedView);
        }

        protected override void OnShutdown()
        {
            Log.Information("Shutting down");
            base.OnShutdown();
        }

        private static void CheckNotNull([NotNull] object? obj)
        {
            if (obj is null)
            {
                throw new InvalidOperationException("Plugin Not Initialized");
            }
        }

        private SqliteDatabaseCollector GetCollector()
        {
            CheckNotNull(collector);
            return collector;
        }

#pragma warning disable CS0618 // Type or member is obsolete

        private async Task HSEventImpl(Constants.HSEvent eventType, object[] parameters)
        {
            try
            {
                if ((eventType == Constants.HSEvent.VALUE_CHANGE) && (parameters.Length > 4))
                {
                    int deviceRefId = Convert.ToInt32(parameters[4], CultureInfo.InvariantCulture);
                    await RecordDeviceValue(deviceRefId, ChangeType.Value).ConfigureAwait(false);
                }
                else if ((eventType == Constants.HSEvent.STRING_CHANGE) && (parameters.Length > 3))
                {
                    int deviceRefId = Convert.ToInt32(parameters[3], CultureInfo.InvariantCulture);
                    await RecordDeviceValue(deviceRefId, ChangeType.String).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to process HSEvent {eventType} with {error}", eventType, ex.GetFullMessage());
            }
        }

#pragma warning restore CS0618 // Type or member is obsolete

        private static bool IsMonitored(AbstractHsDevice feature)
        {
            if (IsTimer(feature))  //ignore timer changes
            {
                return false;
            }

            return true;

            static bool IsTimer(AbstractHsDevice feature)
            {
                if (string.IsNullOrEmpty(feature.Interface))
                {
                    var typeInfo = feature.TypeInfo;
                    if (typeInfo.Summary == "Timer")
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private async Task RecordAllDevices()
        {
            var deviceEnumerator = HomeSeerSystem.GetAllRefs();
            foreach (var refId in deviceEnumerator)
            {
                await RecordDeviceValue(refId, ChangeType.Value).ConfigureAwait(false);
                ShutdownCancellationToken.ThrowIfCancellationRequested();
            }
        }

        private async Task RecordDeviceValue(int deviceRefId, ChangeType trackedType)
        {
            var feature = HomeSeerSystem.GetFeatureByRef(deviceRefId);
            if (feature != null)
            {
                if (IsMonitored(feature))
                {
                    var collector = GetCollector();
                    await RecordDeviceValue(collector, feature).ConfigureAwait(false);
                }
            }
        }

        private static async Task RecordDeviceValue(SqliteDatabaseCollector collector, HsFeature feature)
        {
            ExtractValues(feature, out var deviceValue, out var lastChange, out var deviceString);

            RecordData recordData = new(feature.Ref, deviceValue, deviceString, lastChange);
            Log.Debug("Recording {record}", recordData);

            await collector.Record(recordData).ConfigureAwait(false);

            static void ExtractValues(HsFeature feature, out double deviceValue,
                                                         out DateTimeOffset lastChange,
                                                         out string deviceString)
            {
                deviceValue = feature.Value;
                lastChange = feature.LastChange;
                var type = feature.TypeInfo.ApiType;

                switch (type)
                {
                    default: // older types from HS3
                    case HomeSeer.PluginSdk.Devices.Identification.EApiType.NotSpecified:
                        deviceString = feature.StatusString;
                        if (string.IsNullOrWhiteSpace(deviceString))
                        {
                            if (feature.StatusGraphics.ContainsValue(deviceValue))
                            {
                                var control = feature.StatusGraphics[deviceValue];
                                if (control.IsValueInRange(deviceValue))
                                {
                                    deviceString = control.GetLabelForValue(deviceValue);
                                }
                            }
                        }
                        if (string.IsNullOrWhiteSpace(deviceString))
                        {
                            if (feature.StatusControls.ContainsValue(deviceValue))
                            {
                                var control = feature.StatusControls[deviceValue];
                                if (control.IsValueInRange(deviceValue))
                                {
                                    deviceString = control.GetLabelForValue(deviceValue);
                                }
                            }
                        }
                        break;

                    case HomeSeer.PluginSdk.Devices.Identification.EApiType.Device:
                    case HomeSeer.PluginSdk.Devices.Identification.EApiType.Feature:
                        deviceString = feature.DisplayedStatus;
                        if (string.IsNullOrWhiteSpace(deviceString))
                        {
                            deviceString = feature.StatusString;
                        }
                        break;
                }
            }
        }

        private void RestartProcessing()
        {
            Utils.TaskHelper.StartAsyncWithErrorChecking("All device collection", RecordAllDevices, ShutdownCancellationToken);
        }

        private void UpdateDebugLevel()
        {
            CheckNotNull(settingsPages);

            bool debugLevel = settingsPages.DebugLoggingEnabled;
            bool logToFile = settingsPages.LogtoFileEnabled;
            this.LogDebug = debugLevel;
            Logger.ConfigureLogging(settingsPages.LogLevel, logToFile, HomeSeerSystem);
        }

        private SqliteDatabaseCollector? collector;
        private MonitoredDevicesConfig? monitoredDevicesConfig;
        private SettingsPages? settingsPages;
    }
}