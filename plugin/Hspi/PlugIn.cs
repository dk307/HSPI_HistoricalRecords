﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using Hspi.Database;
using Hspi.DeviceData;
using Hspi.Utils;
using Nito.AsyncEx.Synchronous;
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

        public IDictionary<string, string> GetDatabaseStats()
        {
            return Collector.GetDatabaseStats();
        }

        public override string GetJuiDeviceConfigPage(int devOrFeatRef)
        {
            try
            {
                return CreateDeviceConfigPage(devOrFeatRef, "devicehistoricalrecords.html");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to create page for {deviceRef} with error:{error}", devOrFeatRef, ex.GetFullMessage());
                var page = PageFactory.CreateDeviceConfigPage(PlugInData.PlugInId, PlugInData.PlugInName);
                page = page.WithView(new LabelView("exception", string.Empty, ex.GetFullMessage())
                {
                    LabelType = HomeSeer.Jui.Types.ELabelType.Preformatted
                });
                return page.Page.ToJsonString();
            }
        }

        public IList<KeyValuePair<long, long>> GetTop10RecordsStats()
        {
            return Collector.GetTop10RecordsStats();
        }

        public override bool HasJuiDeviceConfigPage(int devOrFeatRef)
        {
            CheckNotNull(featureCachedDataProvider);
            return featureCachedDataProvider.IsMonitoried(devOrFeatRef);
        }

        public override void HsEvent(Constants.HSEvent eventType, object[] @params)
        {
            try
            {
                HSEventImpl(eventType, @params).WaitAndUnwrapException(ShutdownCancellationToken);
            }
            catch (Exception ex)
            {
                if (ex.IsCancelException())
                {
                    return;
                }
                Log.Warning("Error in recording event with {error}", ex.GetFullMessage());
            }
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
                statisticsDeviceUpdater?.Dispose();
                collector?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void Initialize()
        {
            try
            {
                Log.Information("Plugin Starting");
                featureCachedDataProvider = new HsFeatureCachedDataProvider(HomeSeerSystem);
                Settings.Add(SettingsPages.CreateDefault());
                LoadSettingsFromIni();
                settingsPages = new SettingsPages(HomeSeerSystem, Settings);
                UpdateDebugLevel();

                CheckNotNull(settingsPages);
                collector = new SqliteDatabaseCollector(settingsPages, CreateClock(), ShutdownCancellationToken);
                statisticsDeviceUpdater = new StatisticsDeviceUpdater(HomeSeerSystem, collector, CreateClock(), featureCachedDataProvider, ShutdownCancellationToken);

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, PlugInData.PlugInId);
                HomeSeerSystem.RegisterFeaturePage(this.Id, "dbstats.html", "Database statistics");
                HomeSeerSystem.RegisterDeviceIncPage(this.Id, "adddevice.html", "Add a database statistics device");

                Utils.TaskHelper.StartAsyncWithErrorChecking("All device values collection",
                                                             RecordAllDevices,
                                                             ShutdownCancellationToken);

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

        private async Task HSEventImpl(Constants.HSEvent eventType, object[] parameters)
        {
            if ((eventType == Constants.HSEvent.VALUE_CHANGE) && (parameters.Length > 4))
            {
                int deviceRefId = ConvertToInt32(4);
                await RecordDeviceValue(deviceRefId).ConfigureAwait(false);
            }
            else if ((eventType == Constants.HSEvent.STRING_CHANGE) && (parameters.Length > 3))
            {
                int deviceRefId = ConvertToInt32(3);
                await RecordDeviceValue(deviceRefId).ConfigureAwait(false);
            }
            else if ((eventType == Constants.HSEvent.CONFIG_CHANGE) && (parameters.Length > 3) && ConvertToInt32(1) == 0)
            {
                int refId = ConvertToInt32(3);
                featureCachedDataProvider?.Invalidate(refId);

                const int DeleteDevice = 2;
                if (ConvertToInt32(4) == DeleteDevice && (statisticsDeviceUpdater?.HasRefId(refId) ?? false))
                {
                    RestartStatisticsDeviceUpdate();
                }
            }

            int ConvertToInt32(int index)
            {
                return Convert.ToInt32(parameters[index], CultureInfo.InvariantCulture);
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
            if (IsFeatureTracked(deviceRefId))
            {
                var feature = new HsFeatureData(HomeSeerSystem, deviceRefId);
                await RecordDeviceValue(feature).ConfigureAwait(false);
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

            RecordData recordData = new(feature.Ref, deviceValue, deviceString, lastChange);
            Log.Verbose("Recording {@record}", recordData);

            await collector.Record(recordData).ConfigureAwait(false);
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
        private HsFeatureCachedDataProvider? featureCachedDataProvider;
        private SettingsPages? settingsPages;
    }
}