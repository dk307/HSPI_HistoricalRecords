using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Types;
using Hspi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Serilog;
using Serilog.Events;
using static HomeSeer.PluginSdk.PluginStatus;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class PlugInTest
    {
        [Test]
        public void AllDevicesAreUpdatedOnStart()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);
            List<int> allDeviceRefs = new() { 1000, 1001 };

            mockHsController.SetupFeature(allDeviceRefs[0], 1.1, "abcd", lastChange: DateTime.Now - TimeSpan.FromDays(6));
            mockHsController.SetupFeature(allDeviceRefs[1], 2221.1, "rteyee", lastChange: DateTime.Now - TimeSpan.FromDays(24));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.CheckRecordedValueForFeatureType(plugin, mockHsController.GetFeature(allDeviceRefs[0]), 100, 1);
            TestHelper.CheckRecordedValueForFeatureType(plugin, mockHsController.GetFeature(allDeviceRefs[1]), 100, 1);
        }

        [TestCase(LogEventLevel.Information, false)]
        [TestCase(LogEventLevel.Warning, false)]
        [TestCase(LogEventLevel.Fatal, false)]
        [TestCase(LogEventLevel.Information, true)]
        [TestCase(LogEventLevel.Verbose, false)]
        [TestCase(LogEventLevel.Debug, true)]
        public void CheckLogLevelSettingChange(LogEventLevel logEventLevel, bool logToFile)
        {
            var settingsFromIni = new Dictionary<string, string>
            {
                { "LogLevelId", (logEventLevel == LogEventLevel.Verbose ? LogEventLevel.Fatal : LogEventLevel.Verbose).ToString() },
                { "LogToFileId", logToFile.ToString() }
            };

            TestHelper.CreateMockPlugInAndHsController2(settingsFromIni, out var plugInMock, out var _);
            using PlugInLifeCycle plugInLifeCycle = new(plugInMock);

            PlugIn plugIn = plugInMock.Object;

            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logEventLevel, logToFile)
            };

            Assert.That(plugIn.SaveJuiSettingsPages(settingsCollection.ToJsonString()));

            Assert.That(logEventLevel <= LogEventLevel.Fatal, Is.EqualTo(Log.Logger.IsEnabled(LogEventLevel.Fatal)));
            Assert.That(logEventLevel <= LogEventLevel.Error, Is.EqualTo(Log.Logger.IsEnabled(LogEventLevel.Error)));
            Assert.That(logEventLevel <= LogEventLevel.Warning, Is.EqualTo(Log.Logger.IsEnabled(LogEventLevel.Warning)));
            Assert.That(logEventLevel <= LogEventLevel.Information, Is.EqualTo(Log.Logger.IsEnabled(LogEventLevel.Information)));
            Assert.That(logEventLevel <= LogEventLevel.Debug, Is.EqualTo(Log.Logger.IsEnabled(LogEventLevel.Debug)));
            Assert.That(logEventLevel <= LogEventLevel.Verbose, Is.EqualTo(Log.Logger.IsEnabled(LogEventLevel.Verbose)));
            plugInMock.Verify();
        }

        [Test]
        public void CheckPlugInStatus()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugInMock, out var _);
            using PlugInLifeCycle plugInLifeCycle = new(plugInMock);

            Assert.That(PluginStatus.Ok().Status, Is.EqualTo(plugInMock.Object.OnStatusCheck().Status));
        }

        [Test]
        public void CheckPlugInStatusOnFailedSqliteInitAndRecovers()
        {
            // locking file does not work on ubuntu
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TestHelper.CreateMockPlugInAndHsController2(out var plugInMock, out var hsMockController);

                var mockClock = TestHelper.CreateMockSystemGlobalTimerAndClock(plugInMock);
                mockClock.Setup(x => x.IntervalToRetrySqliteCollection).Returns(TimeSpan.FromMilliseconds(5));

                string dbPath = hsMockController.DBPath;

                Directory.CreateDirectory(Path.GetDirectoryName(dbPath));

                //lock the sqlite 3 db
                var lockFile = new FileStream(dbPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                using PlugInLifeCycle plugInLifeCycle = new(plugInMock);

                var status = plugInMock.Object.OnStatusCheck();

                Assert.That(status.Status, Is.EqualTo(EPluginStatus.Critical));
                Assert.That(status.StatusText, Is.EqualTo("unable to open database file"));

                //unlock file
                lockFile.Dispose();

                Assert.That(TestHelper.TimedWaitTillTrue(() =>
                {
                    return EPluginStatus.Ok == plugInMock.Object.OnStatusCheck().Status;
                }));

                plugInMock.Object.PruneDatabase();
            }
        }

        [Test]
        public void CheckSettingsWithIniFilledDuringInitialize()
        {
            var settingsFromIni = new Dictionary<string, string>()
            {
                { SettingsPages.LoggingLevelId, ((int)LogEventLevel.Information).ToString()},
                { SettingsPages.LogToFileId, true.ToString()},
            };

            TestHelper.CreateMockPlugInAndHsController2(settingsFromIni, out var plugInMock, out var _);
            using PlugInLifeCycle plugInLifeCycle = new(plugInMock);

            PlugIn plugIn = plugInMock.Object;

            Assert.That(plugIn.HasSettings);

            var settingPages = SettingsCollection.FromJsonString(plugIn.GetJuiSettingsPages());
            Assert.That(settingPages, Is.Not.Null);

            var settings = settingPages[SettingsPages.SettingPageId].ToValueMap();

            Assert.That(((int)LogEventLevel.Information).ToString(), Is.EqualTo(settings[SettingsPages.LoggingLevelId]));
            Assert.That(true.ToString(), Is.EqualTo(settings[SettingsPages.LogToFileId]));
        }

        [Test]
        public void GetFeatureUnit()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 5655;
            mockHsController.SetupFeature(refId, 1.132, "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var unit1 = plugin.Object.GetFeatureUnit(refId);
            Assert.That(unit1, Is.EqualTo("F"));

            mockHsController.SetupDevOrFeatureValue(refId, EProperty.DisplayedStatus, "1.1 C");

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, refId, 0 });

            var unit2 = plugin.Object.GetFeatureUnit(refId);
            Assert.That(unit2, Is.EqualTo("C"));
        }

        [TestCase("96%", "%")]
        [TestCase("96 %", "%")]
        [TestCase("-962 W", "W")]
        [TestCase("+962 W", "W")]
        [TestCase("213.00 C", "C")]
        [TestCase("93dkfe6 W", null)]
        [TestCase("96 kW hours", "kW hours")]
        [TestCase("96 F", "F")]
        [TestCase("96234857", null)]
        [TestCase("apple", null)]
        public void GetFeatureUnitForDifferentTypes(string displayStatus, string unit)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            mockHsController.SetupFeature(100, 0);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            mockHsController.SetupDevOrFeatureValue(100, EProperty.DisplayedStatus, displayStatus);
            var unitFound = plugin.Object.GetFeatureUnit(100);
            Assert.That(unitFound, Is.EqualTo(unit));
        }

        [Test]
        public void GetJuiDeviceConfigPageErrored()
        {
            TestHelper.CreateMockPlugInAndMoqHsController(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int deviceRefId = 10;

            string errorMessage = "sdfsd dfgdfg erter";
            mockHsController.Setup(x => x.GetPropertyByRef(deviceRefId, It.IsAny<EProperty>())).Throws(new Exception(errorMessage));

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(deviceRefId);

            var result = (JObject)JsonConvert.DeserializeObject(pageJson);

            Assert.That(result, Is.Not.Null);
            Assert.That((string)result["views"][0]["value"], Is.EqualTo(errorMessage));
        }

        [TestCase(PlugInData.PlugInId, "editdevice")]
        [TestCase("", "history")]
        public void GetJuiDeviceConfigPageForDevice(string deviceInterface, string page)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);
            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devOrFeatRef = 10;

            mockHsController.SetupDevice(devOrFeatRef, deviceInterface: deviceInterface);

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(devOrFeatRef);

            var data = (JObject)JsonConvert.DeserializeObject(pageJson);
            Assert.That(data, Is.Not.Null);
            Assert.That(data["type"].Value<int>(), Is.EqualTo(5));

            string labelHtml = data["views"][0]["name"].Value<string>();

            var htmlDoc = TestHelper.VerifyHtmlValid(labelHtml);
            var iFrameElement = htmlDoc.GetElementbyId("pluginhistoryiframeid");

            Assert.That(iFrameElement, Is.Not.Null);

            var iFrameSource = iFrameElement.Attributes["src"].Value;
            Assert.That($"/History/{page}.html?ref={devOrFeatRef}&feature={devOrFeatRef}", Is.EqualTo(iFrameSource));
        }

        [Test]
        [TestCase(PlugInData.PlugInId, "editdevice")]
        [TestCase("", "history")]
        public void GetJuiDeviceConfigPageForFeature(string deviceInterface, string page)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);
            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devOrFeatRef = 10;
            mockHsController.SetupFeature(devOrFeatRef, 0, featureInterface: deviceInterface);
            mockHsController.SetupDevOrFeatureValue(devOrFeatRef, EProperty.AssociatedDevices, new HashSet<int>() { 9 });

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(devOrFeatRef);

            var data = (JObject)JsonConvert.DeserializeObject(pageJson);
            Assert.That(data, Is.Not.Null);
            Assert.That(data["type"].Value<int>(), Is.EqualTo(5));

            string labelHtml = data["views"][0]["name"].Value<string>();

            var htmlDoc = TestHelper.VerifyHtmlValid(labelHtml);
            var iFrameElement = htmlDoc.GetElementbyId("pluginhistoryiframeid");

            Assert.That(iFrameElement, Is.Not.Null);

            var iFrameSource = iFrameElement.Attributes["src"].Value;
            Assert.That(iFrameSource, Is.EqualTo($"/History/{page}.html?ref={9}&feature={devOrFeatRef}"));
        }

        [Test]
        public void GetPrecisionInvalidation()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 10394;
            mockHsController.SetupFeature(refId, 1.1373646, "1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            List<StatusGraphic> statusGraphics1 = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 3 }) };
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, statusGraphics1);

            var precision1 = plugin.Object.GetFeaturePrecision(refId);
            Assert.That(precision1, Is.EqualTo(3));

            List<StatusGraphic> statusGraphics2 = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, statusGraphics2);

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, refId, 0 });

            var precision2 = plugin.Object.GetFeaturePrecision(refId);
            Assert.That(precision2, Is.EqualTo(1));
        }

        [TestCase(null, 3, 3)]
        [TestCase("0 Watts", null, 3)]
        [TestCase("1.0", 3, 3)]
        [TestCase("-1.3382", 1, 4)]
        [TestCase("1.0", null, 1)]
        [TestCase("1 Volt", 5, 5)]
        [TestCase(null, null, 3)]
        public void GetPrecision(string displayStatus, int? graphicsMaxPrecision, int expectedPrecision)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 10394;
            mockHsController.SetupFeature(refId, 1.132, displayStatus);

            if (graphicsMaxPrecision.HasValue)
            {
                List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = graphicsMaxPrecision.Value }) };
                mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, statusGraphics);
            }

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var precision1 = plugin.Object.GetFeaturePrecision(refId);
            Assert.That(precision1, Is.EqualTo(expectedPrecision));
        }

        [Test]
        public void InitFirstTime()
        {
            TestHelper.CreateMockPlugInAndMoqHsController(out var plugin, out var mockHsController);

            Assert.That(plugin.Object.InitIO());
            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();

            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterEventCB((Constants.HSEvent)0x200, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterFeaturePage(PlugInData.PlugInId, "dbstats.html", "Database statistics"));
            mockHsController.Verify(x => x.RegisterFeaturePage(PlugInData.PlugInId, "alldevices.html", "Device statistics"));
            mockHsController.Verify(x => x.RegisterDeviceIncPage(PlugInData.PlugInId, "adddevice.html", "Add a statistics device"));

            string dbPath = Path.Combine(mockHsController.Object.GetAppPath(), "data", PlugInData.PlugInId, "records.db");
            Assert.That(File.Exists(dbPath));
        }

        [Test]
        public void IsFeatureTrackedForTimer()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 9456;
            mockHsController.SetupFeature(refId, 1.132, "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var tracked1 = plugin.Object.IsFeatureTracked(refId);
            Assert.That(tracked1);
            Assert.That(plugin.Object.HasJuiDeviceConfigPage(refId));

            var data = new PlugExtraData();
            data.AddNamed("timername", "123");
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.PlugExtraData, data);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.Interface, string.Empty);

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, refId, 0 });

            var tracked2 = plugin.Object.IsFeatureTracked(refId);
            Assert.That(!tracked2);

            Assert.That(!plugin.Object.HasJuiDeviceConfigPage(refId));
        }

        [Test]
        public void PostBackProcforNonHandled()
        {
            var plugin = new PlugIn();
            Assert.That(string.Empty, Is.EqualTo(plugin.PostBackProc("Random", "data", "user", 0)));
        }

        [Test]
        public void UseWithoutInit()
        {
            var plugin = new PlugIn();
            Assert.Catch<InvalidOperationException>(() => plugin.PruneDatabase());
            Assert.Catch<InvalidOperationException>(() => plugin.DeleteAllRecords(10));
            Assert.Catch<InvalidOperationException>(() => plugin.GetDatabaseStats());
        }

        [Test]
        public void VerifyAccessLevel()
        {
            var plugin = new PlugIn();
            Assert.That(plugin.AccessLevel, Is.EqualTo((int)EAccessLevel.RequiresLicense));
        }

        [Test]
        public void VerifyNameAndId()
        {
            var plugin = new PlugIn();
            Assert.That(plugin.Id, Is.EqualTo(PlugInData.PlugInId));
            Assert.That(plugin.Name, Is.EqualTo(PlugInData.PlugInName));
        }

        [Test]
        public void VerifySupportsConfigDeviceAll()
        {
            var plugin = new PlugIn();
            Assert.That(plugin.SupportsConfigDeviceAll);
            Assert.That(plugin.SupportsConfigFeature);
            Assert.That(plugin.SupportsConfigDevice);
        }
    }
}