using System;
using System.IO;
using System.Linq;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using Hspi.Utils;
using Serilog.Events;

#nullable enable

namespace Hspi
{
    internal sealed class SettingsPages : Database.IDBSettings
    {
        public SettingsPages(IHsController hs, SettingsCollection collection)
        {
            this.dbPath = Path.Combine(hs.GetAppPath(), "data", PlugInData.PlugInId, "records.db");

            if (!Enum.TryParse<LogEventLevel>(collection[SettingPageId].GetViewById<SelectListView>(LoggingLevelId).GetSelectedOptionKey(), out LogEventLevel logEventLevel))
            {
                LogLevel = LogEventLevel.Information;
            }
            else
            {
                LogLevel = logEventLevel;
            }

            LogtoFileEnabled = collection[SettingPageId].GetViewById<ToggleView>(LogToFileId).IsEnabled;
            GlobalRetentionPeriod = collection[SettingPageId].GetViewById<TimeSpanView>(GlobalRetentionPeriodId).Value;
            this.perDeviceSettingsConfig = new PerDeviceSettingsConfig(hs);
        }

        public static TimeSpan DefaultGlobalRetentionPeriod => TimeSpan.FromDays(30);

        public string DBPath => dbPath;

        public bool DebugLoggingEnabled => LogLevel <= LogEventLevel.Debug;

        public TimeSpan GlobalRetentionPeriod { get; private set; }

        public LogEventLevel LogLevel { get; private set; }

        public bool LogtoFileEnabled { get; private set; }

        public bool LogValueChangeEnabled { get; private set; }

        public long MinRecordsToKeep => MinRecordsToKeepDefault;

        public static Page CreateDefault(LogEventLevel logEventLevel = LogEventLevel.Information,
                                         bool logToFileDefault = false,
                                         TimeSpan? globalDefaultRetention = null)
        {
            var settings = PageFactory.CreateSettingsPage(SettingPageId, "Settings");

            var spanView = new TimeSpanView(GlobalRetentionPeriodId, "Records retention period")
            {
                ShowDays = true,
                ShowSeconds = false,
                Value = globalDefaultRetention ?? DefaultGlobalRetentionPeriod
            };

            settings.Page.AddView(spanView);

            var logOptions = EnumHelper.GetValues<LogEventLevel>().Select(x => x.ToString()).ToList();
            settings = settings.WithDropDownSelectList(LoggingLevelId, "Logging Level", logOptions, logOptions, (int)logEventLevel);

            settings = settings.WithToggle(LogToFileId, "Log to file", logToFileDefault);
            return settings.Page;
        }

        public void AddOrUpdate(PerDeviceSettings device) => perDeviceSettingsConfig.AddOrUpdate(device);

        public void Remove(int deviceRefId) => perDeviceSettingsConfig.Remove(deviceRefId);

        public TimeSpan GetDeviceRetentionPeriod(long deviceRefId)
        {
            if (this.perDeviceSettingsConfig.DeviceSettings.TryGetValue(deviceRefId, out var result))
            {
                return result.RetentionPeriod ?? GlobalRetentionPeriod;
            }

            return GlobalRetentionPeriod;
        }

        public bool IsTracked(long deviceRefId)
        {
            if (this.perDeviceSettingsConfig.DeviceSettings.TryGetValue(deviceRefId, out var result))
            {
                return result.IsTracked;
            }
            return true;
        }

        public bool OnSettingChange(AbstractView changedView)
        {
            if (changedView.Id == LoggingLevelId)
            {
                var value = ((SelectListView)changedView).GetSelectedOptionKey();
                if (Enum.TryParse<LogEventLevel>(value, out LogEventLevel logEventLevel))
                {
                    LogLevel = logEventLevel;
                    return true;
                }
                return false;
            }

            if (changedView.Id == LogToFileId)
            {
                LogtoFileEnabled = ((ToggleView)changedView).IsEnabled;
                return true;
            }

            if (changedView.Id == GlobalRetentionPeriodId)
            {
                var value = ((TimeSpanView)changedView).Value;
                if (value > TimeSpan.Zero)
                {
                    GlobalRetentionPeriod = value;
                    return true;
                }

                return false;
            }

            return false;
        }

        public const int MinRecordsToKeepDefault = 100;
        internal const string GlobalRetentionPeriodId = "GlobalRetentionPeriod";
        internal const string LoggingLevelId = "LogLevel";
        internal const string LogToFileId = "LogToFile";
        internal const string SettingPageId = "SettingPage";
        private readonly string dbPath;
        private readonly PerDeviceSettingsConfig perDeviceSettingsConfig;
    }
}