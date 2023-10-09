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

        public override bool SupportsConfigDeviceAll => true;
        public override bool SupportsConfigFeature => true;

        public override string GetJuiDeviceConfigPage(int deviceRef)
        {
            try
            {
                var device = HomeSeerSystem.GetFeatureByRef(deviceRef);
                return CreateDeviceConfigPage(device, "devicehistoricalrecords.html");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to create page for {deviceRef} with error:{error}", deviceRef, ex.GetFullMessage());
                var page = PageFactory.CreateDeviceConfigPage(PlugInData.PlugInId, PlugInData.PlugInName);
                page = page.WithView(new LabelView("exception", string.Empty, ex.GetFullMessage())
                {
                    LabelType = HomeSeer.Jui.Types.ELabelType.Preformatted
                });
                return page.Page.ToJsonString();
            }
        }

        public override void HsEvent(Constants.HSEvent eventType, object[] parameters)
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
                collector?.Dispose();
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
                // monitoredDevicesConfig = new MonitoredDevicesConfig(HomeSeerSystem);
                UpdateDebugLevel();

                string dbPath = Path.Combine(HomeSeerSystem.GetAppPath(), "data", PlugInData.PlugInId, "records.db");

                // string dbPath2 = Path.Combine(Path.GetTempPath(), "test2.db");
                collector = new SqliteDatabaseCollector(dbPath, ShutdownCancellationToken);

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId);

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

        private SqliteDatabaseCollector Collector
        {
            get
            {
                CheckNotNull(collector);
                return collector;
            }
        }

        private async Task HSEventImpl(Constants.HSEvent eventType, object[] parameters)
        {
            try
            {
                if ((eventType == Constants.HSEvent.VALUE_CHANGE) && (parameters.Length > 4))
                {
                    int deviceRefId = Convert.ToInt32(parameters[4], CultureInfo.InvariantCulture);
                    await RecordDeviceValue(deviceRefId).ConfigureAwait(false);
                }
                else if ((eventType == Constants.HSEvent.STRING_CHANGE) && (parameters.Length > 3))
                {
                    int deviceRefId = Convert.ToInt32(parameters[3], CultureInfo.InvariantCulture);
                    await RecordDeviceValue(deviceRefId).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to process HSEvent {eventType} with {error}", eventType, ex.GetFullMessage());
            }
        }

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
                await RecordDeviceValue(refId).ConfigureAwait(false);
                ShutdownCancellationToken.ThrowIfCancellationRequested();
            }
        }

        private async Task RecordDeviceValue(int deviceRefId)
        {
            var feature = HomeSeerSystem.GetFeatureByRef(deviceRefId);
            if (feature != null)
            {
                if (IsMonitored(feature))
                {
                    await RecordDeviceValue(feature).ConfigureAwait(false);
                }
            }
        }

        private async Task RecordDeviceValue(HsFeature feature)
        {
            CheckNotNull(collector);

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
            Utils.TaskHelper.StartAsyncWithErrorChecking("All device values collection",
                                                         RecordAllDevices,
                                                         ShutdownCancellationToken);
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