using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.Jui.Types;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using Hspi;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog.Events;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class SettingsPagesTest
    {
        public SettingsPagesTest()
        {
            this.plugin = TestHelper.CreatePlugInMock();
            this.mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());
        }

        [TestMethod]
        public void AddRemovePerDeviceSettings()
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault()
            };
            PerDeviceSettings deviceSettings = new(837, false, TimeSpan.FromSeconds(666), 1.0, 99.0);
            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RefId", deviceSettings.DeviceRefId.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "IsTracked", deviceSettings.IsTracked.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RetentionPeriod", deviceSettings.RetentionPeriod.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MinValue", deviceSettings.MinValue.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MaxValue", deviceSettings.MaxValue.ToString(), PlugInData.SettingFileName));

            settingPages.AddOrUpdate(deviceSettings);

            Assert.IsFalse(settingPages.IsTracked(deviceSettings.DeviceRefId));
            Assert.AreEqual(settingPages.GetDeviceRetentionPeriod(deviceSettings.DeviceRefId), deviceSettings.RetentionPeriod.Value);
            mockHsController.Verify();

            mockHsController.Setup(x => x.ClearIniSection(deviceSettings.DeviceRefId.ToString(), PlugInData.SettingFileName));

            settingPages.Remove((int)deviceSettings.DeviceRefId);
            Assert.IsTrue(settingPages.IsTracked(deviceSettings.DeviceRefId));
            Assert.AreEqual(settingPages.GetDeviceRetentionPeriod(deviceSettings.DeviceRefId), settingPages.GlobalRetentionPeriod);

            mockHsController.Verify();

            var deviceSettings2 = deviceSettings with { RetentionPeriod = null };

            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RefId", deviceSettings2.DeviceRefId.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "IsTracked", deviceSettings2.IsTracked.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RetentionPeriod", string.Empty, PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MinValue", deviceSettings2.MinValue.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MaxValue", deviceSettings2.MaxValue.ToString(), PlugInData.SettingFileName));

            settingPages.AddOrUpdate(deviceSettings2);

            Assert.AreEqual(settingPages.GetDeviceRetentionPeriod(deviceSettings2.DeviceRefId), settingPages.GlobalRetentionPeriod);
            mockHsController.Verify();

            var deviceSettings3 = deviceSettings with { RetentionPeriod = null, MinValue = null, MaxValue = null };

            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RefId", deviceSettings3.DeviceRefId.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "IsTracked", deviceSettings3.IsTracked.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RetentionPeriod", string.Empty, PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MinValue", string.Empty, PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MaxValue", string.Empty, PlugInData.SettingFileName));

            settingPages.AddOrUpdate(deviceSettings3);
            mockHsController.Verify();
        }

        [TestMethod]
        public void CreateDefault()
        {
            var page = SettingsPages.CreateDefault();

            Assert.IsNotNull(page);

            foreach (var view in page.Views)
            {
                TestHelper.VerifyHtmlValid(view.ToHtml());
            }

            TestHelper.VerifyHtmlValid(page.ToHtml());

            Assert.IsTrue(page.ContainsViewWithId(SettingsPages.LogToFileId));
            Assert.IsTrue(page.ContainsViewWithId(SettingsPages.LoggingLevelId));
        }

        [DataTestMethod]
        [DataRow(LogEventLevel.Information, false, 35)]
        [DataRow(LogEventLevel.Warning, false, 45677)]
        [DataRow(LogEventLevel.Fatal, false, 99999)]
        [DataRow(LogEventLevel.Information, true, 10000)]
        [DataRow(LogEventLevel.Verbose, false, 100000)]
        [DataRow(LogEventLevel.Debug, true, 60 * 24 * 30)]
        public void DefaultValues(LogEventLevel logEventLevel,
                                  bool logToFileEnable,
                                  long globalRetentionPeriodSeconds)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logEventLevel, logToFileEnable, TimeSpan.FromSeconds(globalRetentionPeriodSeconds))
            };

            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            Assert.AreEqual(settingPages.LogLevel, logEventLevel);
            Assert.AreEqual(settingPages.DebugLoggingEnabled, logEventLevel <= LogEventLevel.Debug);
            Assert.AreEqual(settingPages.LogtoFileEnabled, logToFileEnable);
            Assert.AreEqual(settingPages.GlobalRetentionPeriod, TimeSpan.FromSeconds(globalRetentionPeriodSeconds));
        }

        [DataTestMethod]
        [DataRow(60)]
        [DataRow(60 * 24)]
        [DataRow(60 * 24 * 7)]
        [DataRow(60 * 24 * 365)]
        public void OnSettingChangeWithGlobalRetentionPeriod(long seconds)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(globalDefaultRetention: SettingsPages.DefaultGlobalRetentionPeriod)
            };
            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            EnumHelper.GetValues<LogEventLevel>().Select(x => x.ToString()).ToList();
            TimeSpanView changedView = new(SettingsPages.GlobalRetentionPeriodId, "name")
            {
                Value = TimeSpan.FromSeconds(seconds)
            };

            Assert.IsTrue(settingPages.OnSettingChange(changedView));
            Assert.AreEqual(settingPages.GlobalRetentionPeriod, TimeSpan.FromSeconds(seconds));
        }

        [DataTestMethod]
        [DataRow(LogEventLevel.Fatal)]
        [DataRow(LogEventLevel.Warning)]
        [DataRow(LogEventLevel.Error)]
        [DataRow(LogEventLevel.Information)]
        [DataRow(LogEventLevel.Debug)]
        [DataRow(LogEventLevel.Verbose)]
        public void OnSettingChangeWithLogLevelChange(LogEventLevel logEventLevel)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logEventLevel: (logEventLevel == LogEventLevel.Verbose ? LogEventLevel.Fatal : LogEventLevel.Verbose))
            };
            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            var logOptions = EnumHelper.GetValues<LogEventLevel>().Select(x => x.ToString()).ToList();
            SelectListView changedView = new(SettingsPages.LoggingLevelId, "name", logOptions, logOptions, ESelectListType.DropDown,
                                             (int)logEventLevel);
            Assert.IsTrue(settingPages.OnSettingChange(changedView));
            Assert.AreEqual(settingPages.LogLevel, logEventLevel);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void OnSettingChangeWithLogtoFileChange(bool initialValue)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logToFileDefault: initialValue)
            };
            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            ToggleView changedView = new(SettingsPages.LogToFileId, "name", !initialValue);
            Assert.IsTrue(settingPages.OnSettingChange(changedView));
            Assert.AreEqual(settingPages.LogtoFileEnabled, !initialValue);
        }

        [TestMethod]
        public void OnSettingChangeWithNoChange()
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault()
            };
            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            Assert.IsFalse(settingPages.OnSettingChange(new ToggleView("id", "name")));
        }

        [TestMethod]
        public void PerDeviceSettingsAreLoaded()
        {
            int deviceRefId = 1592;

            mockHsController.Setup(x => x.GetINISetting("Settings", "DeviceSettings", null, PlugInData.SettingFileName)).Returns(deviceRefId.ToString());
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "RefId", null, PlugInData.SettingFileName)).Returns(deviceRefId.ToString());
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "IsTracked", null, PlugInData.SettingFileName)).Returns(false.ToString());
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "RetentionPeriod", null, PlugInData.SettingFileName)).Returns(TimeSpan.FromMinutes(1).ToString());
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "MinValue", null, PlugInData.SettingFileName)).Returns("0.1");
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "MaxValue", null, PlugInData.SettingFileName)).Returns("100.1");

            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault()
            };

            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            Assert.IsFalse(settingPages.IsTracked(deviceRefId));
            Assert.AreEqual(settingPages.GetDeviceRetentionPeriod(deviceRefId), TimeSpan.FromMinutes(1));
            Assert.AreEqual(0.1D, settingPages.GetDeviceRangeForValidValues(deviceRefId).Item1.Value);
            Assert.AreEqual(100.1D, settingPages.GetDeviceRangeForValidValues(deviceRefId).Item2.Value );
            mockHsController.Verify();
        }

        private readonly Mock<IHsController> mockHsController;
        private readonly Mock<PlugIn> plugin;
    }
}