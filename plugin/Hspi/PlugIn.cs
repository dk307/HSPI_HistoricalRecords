using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
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

        private SqliteDatabaseCollector Collector
        {
            get
            {
                CheckNotNull(collector);
                return collector;
            }
        }

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
            Task.Run(() => HSEventImpl(eventType, parameters)).Wait(ShutdownCancellationToken);
        }

        public void PruneDatabase() => Collector.PruneNow();

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
                settingsPages = new SettingsPages(HomeSeerSystem, Settings);
                UpdateDebugLevel();

                CheckNotNull(settingsPages);
                collector = new SqliteDatabaseCollector(settingsPages, CreateClock(), ShutdownCancellationToken);

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

        private static bool IsMonitored(HsFeatureData feature)
        {
            if (IsTimer(feature))  //ignore timer changes
            {
                return false;
            }

            return true;

            static bool IsTimer(HsFeatureData feature)
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
            if (settingsPages != null && settingsPages.IsTracked(deviceRefId))
            {
                var feature = new HsFeatureData(HomeSeerSystem, deviceRefId);
                if (feature != null)
                {
                    if (IsMonitored(feature))
                    {
                        await RecordDeviceValue(feature).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                Log.Verbose("Not adding {refId} to db as it is not tracked", deviceRefId);
            }
        }

        private async Task RecordDeviceValue(HsFeatureData feature)
        {
            CheckNotNull(collector);

            var deviceValue = feature.Value;
            var lastChange = feature.LastChange;
            var deviceString = feature.DisplayedStatus;

            RecordData recordData = new(feature.DeviceRef, deviceValue, deviceString, lastChange);
            Log.Debug("Recording {@record}", recordData);

            await collector.Record(recordData).ConfigureAwait(false);
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
        private SettingsPages? settingsPages;
    }
}