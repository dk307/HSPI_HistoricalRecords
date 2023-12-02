using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.Jui.Types;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using Hspi;
using Hspi.Utils;
using NUnit.Framework;
using Moq;
using Serilog.Events;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class SettingsPagesTest
    {
        public SettingsPagesTest()
        {
            this.plugin = TestHelper.CreatePlugInMock();
            this.mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());
        }

        [Test]
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

            Assert.That(!settingPages.IsTracked(deviceSettings.DeviceRefId));
            Assert.That(deviceSettings.RetentionPeriod.Value, Is.EqualTo(settingPages.GetDeviceRetentionPeriod(deviceSettings.DeviceRefId)));
            mockHsController.Verify();

            mockHsController.Setup(x => x.ClearIniSection(deviceSettings.DeviceRefId.ToString(), PlugInData.SettingFileName));

            settingPages.Remove((int)deviceSettings.DeviceRefId);
            Assert.That(settingPages.IsTracked(deviceSettings.DeviceRefId));
            Assert.That(settingPages.GlobalRetentionPeriod, Is.EqualTo(settingPages.GetDeviceRetentionPeriod(deviceSettings.DeviceRefId)));

            mockHsController.Verify();

            var deviceSettings2 = deviceSettings with { RetentionPeriod = null };

            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RefId", deviceSettings2.DeviceRefId.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "IsTracked", deviceSettings2.IsTracked.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "RetentionPeriod", string.Empty, PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MinValue", deviceSettings2.MinValue.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceSettings.DeviceRefId.ToString(), "MaxValue", deviceSettings2.MaxValue.ToString(), PlugInData.SettingFileName));

            settingPages.AddOrUpdate(deviceSettings2);

            Assert.That(settingPages.GlobalRetentionPeriod, Is.EqualTo(settingPages.GetDeviceRetentionPeriod(deviceSettings2.DeviceRefId)));
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

        [Test]
        public void CreateDefault()
        {
            var page = SettingsPages.CreateDefault();

            Assert.That(page, Is.Not.Null);

            foreach (var view in page.Views)
            {
                TestHelper.VerifyHtmlValid(view.ToHtml());
            }

            TestHelper.VerifyHtmlValid(page.ToHtml());

            Assert.That(page.ContainsViewWithId(SettingsPages.LogToFileId));
            Assert.That(page.ContainsViewWithId(SettingsPages.LoggingLevelId));
        }

        [TestCase(LogEventLevel.Information, false, 35)]
        [TestCase(LogEventLevel.Warning, false, 45677)]
        [TestCase(LogEventLevel.Fatal, false, 99999)]
        [TestCase(LogEventLevel.Information, true, 10000)]
        [TestCase(LogEventLevel.Verbose, false, 100000)]
        [TestCase(LogEventLevel.Debug, true, 60 * 24 * 30)]
        public void DefaultValues(LogEventLevel logEventLevel,
                                  bool logToFileEnable,
                                  long globalRetentionPeriodSeconds)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logEventLevel, logToFileEnable, TimeSpan.FromSeconds(globalRetentionPeriodSeconds))
            };

            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            Assert.That(logEventLevel, Is.EqualTo(settingPages.LogLevel));
            Assert.That(logEventLevel <= LogEventLevel.Debug, Is.EqualTo(settingPages.DebugLoggingEnabled));
            Assert.That(logToFileEnable, Is.EqualTo(settingPages.LogtoFileEnabled));
            Assert.That(TimeSpan.FromSeconds(globalRetentionPeriodSeconds), Is.EqualTo(settingPages.GlobalRetentionPeriod));
        }

        [TestCase(60)]
        [TestCase(60 * 24)]
        [TestCase(60 * 24 * 7)]
        [TestCase(60 * 24 * 365)]
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

            Assert.That(settingPages.OnSettingChange(changedView));
            Assert.That(TimeSpan.FromSeconds(seconds), Is.EqualTo(settingPages.GlobalRetentionPeriod));
        }

        [TestCase(LogEventLevel.Fatal)]
        [TestCase(LogEventLevel.Warning)]
        [TestCase(LogEventLevel.Error)]
        [TestCase(LogEventLevel.Information)]
        [TestCase(LogEventLevel.Debug)]
        [TestCase(LogEventLevel.Verbose)]
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
            Assert.That(settingPages.OnSettingChange(changedView));
            Assert.That(settingPages.LogLevel, Is.EqualTo(logEventLevel));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OnSettingChangeWithLogtoFileChange(bool initialValue)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logToFileDefault: initialValue)
            };
            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            ToggleView changedView = new(SettingsPages.LogToFileId, "name", !initialValue);
            Assert.That(settingPages.OnSettingChange(changedView));
            Assert.That(!initialValue, Is.EqualTo(settingPages.LogtoFileEnabled));
        }

        [Test]
        public void OnSettingChangeWithNoChange()
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault()
            };
            var settingPages = new SettingsPages(mockHsController.Object, settingsCollection);

            Assert.That(!settingPages.OnSettingChange(new ToggleView("id", "name")));
        }

        [Test]
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

            Assert.That(!settingPages.IsTracked(deviceRefId));
            Assert.That(TimeSpan.FromMinutes(1), Is.EqualTo(settingPages.GetDeviceRetentionPeriod(deviceRefId)));
            Assert.That(settingPages.GetDeviceRangeForValidValues(deviceRefId).Item1.Value, Is.EqualTo(0.1D));
            Assert.That(settingPages.GetDeviceRangeForValidValues(deviceRefId).Item2.Value, Is.EqualTo(100.1D));
            mockHsController.Verify();
        }

        private readonly Mock<IHsController> mockHsController;
        private readonly Mock<PlugIn> plugin;
    }
}