using System;
using System.Collections.Generic;
using System.IO;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Types;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using static HomeSeer.PluginSdk.PluginStatus;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class PlugInTest
    {
        [TestMethod]
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

        [DataTestMethod]
        [DataRow(LogEventLevel.Information, false)]
        [DataRow(LogEventLevel.Warning, false)]
        [DataRow(LogEventLevel.Fatal, false)]
        [DataRow(LogEventLevel.Information, true)]
        [DataRow(LogEventLevel.Verbose, false)]
        [DataRow(LogEventLevel.Debug, true)]
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

            Assert.IsTrue(plugIn.SaveJuiSettingsPages(settingsCollection.ToJsonString()));

            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Fatal), logEventLevel <= LogEventLevel.Fatal);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Error), logEventLevel <= LogEventLevel.Error);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Warning), logEventLevel <= LogEventLevel.Warning);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Information), logEventLevel <= LogEventLevel.Information);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Debug), logEventLevel <= LogEventLevel.Debug);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Verbose), logEventLevel <= LogEventLevel.Verbose);
            plugInMock.Verify();
        }

        [TestMethod]
        public void CheckPlugInStatus()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugInMock, out var _);
            using PlugInLifeCycle plugInLifeCycle = new(plugInMock);

            Assert.AreEqual(plugInMock.Object.OnStatusCheck().Status, PluginStatus.Ok().Status);
        }

        [TestMethod]
        public void CheckPlugInStatusOnFailedSqliteInit()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugInMock, out var hsMockController);

            string dbPath = Path.Combine((hsMockController as IHsController).GetAppPath(), "data", PlugInData.PlugInId, "records.db");

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));

            //lock the sqlite 3 db
            using var lockFile = new FileStream(dbPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            using PlugInLifeCycle plugInLifeCycle = new(plugInMock);

            var status = plugInMock.Object.OnStatusCheck();

            Assert.AreEqual(EPluginStatus.Critical, status.Status);
            Assert.AreEqual("Unable to open database file", status.StatusText);
        }

        [TestMethod]
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

            Assert.IsTrue(plugIn.HasSettings);

            var settingPages = SettingsCollection.FromJsonString(plugIn.GetJuiSettingsPages());
            Assert.IsNotNull(settingPages);

            var settings = settingPages[SettingsPages.SettingPageId].ToValueMap();

            Assert.AreEqual(settings[SettingsPages.LoggingLevelId], ((int)LogEventLevel.Information).ToString());
            Assert.AreEqual(settings[SettingsPages.LogToFileId], true.ToString());
        }

        [TestMethod]
        public void GetFeatureUnit()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 5655;
            mockHsController.SetupFeature(refId, 1.132, "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var unit1 = plugin.Object.GetFeatureUnit(refId);
            Assert.AreEqual("F", unit1);

            mockHsController.SetupDevOrFeatureValue(refId, EProperty.DisplayedStatus, "1.1 C");

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, refId, 0 });

            var unit2 = plugin.Object.GetFeatureUnit(refId);
            Assert.AreEqual("C", unit2);
        }

        [DataTestMethod]
        [DataRow("96%", "%")]
        [DataRow("96 %", "%")]
        [DataRow("-96 W", "W")]
        [DataRow("93dkfe6 W", null)]
        [DataRow("96 kW hours", "kW hours")]
        [DataRow("96 F", "F")]
        [DataRow("96234857", null)]
        [DataRow("apple", null)]
        public void GetFeatureUnitForDifferentTypes(string displayStatus, string unit)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            mockHsController.SetupFeature(100, 0);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            mockHsController.SetupDevOrFeatureValue(100, EProperty.DisplayedStatus, displayStatus);
            var unitFound = plugin.Object.GetFeatureUnit(100);
            Assert.AreEqual(unit, unitFound);
        }

        [TestMethod]
        public void GetJuiDeviceConfigPageErrored()
        {
            TestHelper.CreateMockPlugInAndMoqHsController(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int deviceRefId = 10;

            string errorMessage = "sdfsd dfgdfg erter";
            mockHsController.Setup(x => x.GetFeatureByRef(deviceRefId)).Throws(new Exception(errorMessage));

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(deviceRefId);

            var result = (JObject)JsonConvert.DeserializeObject(pageJson);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["views"][0]["value"], errorMessage);
        }

        [DataTestMethod]
        [DataRow(PlugInData.PlugInId, "editdevice")]
        [DataRow("", "devicehistoricalrecords")]
        public void GetJuiDeviceConfigPageForDevice(string deviceInterface, string page)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);
            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devOrFeatRef = 10;

            mockHsController.SetupDevice(devOrFeatRef, deviceInterface: deviceInterface);

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(devOrFeatRef);

            var data = (JObject)JsonConvert.DeserializeObject(pageJson);
            Assert.IsNotNull(data);
            Assert.AreEqual(5, data["type"].Value<int>());

            string labelHtml = data["views"][0]["name"].Value<string>();

            var htmlDoc = TestHelper.VerifyHtmlValid(labelHtml);
            var iFrameElement = htmlDoc.GetElementbyId("historicalrecordsiframeid");

            Assert.IsNotNull(iFrameElement);

            var iFrameSource = iFrameElement.Attributes["src"].Value;
            Assert.AreEqual(iFrameSource, $"/HistoricalRecords/{page}.html?ref={devOrFeatRef}&feature={devOrFeatRef}");
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow(PlugInData.PlugInId, "editdevice")]
        [DataRow("", "devicehistoricalrecords")]
        public void GetJuiDeviceConfigPageForFeature(string deviceInterface, string page)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);
            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devOrFeatRef = 10;
            mockHsController.SetupFeature(devOrFeatRef, 0, featureInterface: deviceInterface);
            mockHsController.SetupDevOrFeatureValue(devOrFeatRef, EProperty.AssociatedDevices, new HashSet<int>() { 9 });

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(devOrFeatRef);

            var data = (JObject)JsonConvert.DeserializeObject(pageJson);
            Assert.IsNotNull(data);
            Assert.AreEqual(5, data["type"].Value<int>());

            string labelHtml = data["views"][0]["name"].Value<string>();

            var htmlDoc = TestHelper.VerifyHtmlValid(labelHtml);
            var iFrameElement = htmlDoc.GetElementbyId("historicalrecordsiframeid");

            Assert.IsNotNull(iFrameElement);

            var iFrameSource = iFrameElement.Attributes["src"].Value;
            Assert.AreEqual(iFrameSource, $"/HistoricalRecords/{page}.html?ref={9}&feature={devOrFeatRef}");
        }

        [TestMethod]
        public void GetPrecision()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 10394;
            mockHsController.SetupFeature(refId, 1.132, "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var precision1 = plugin.Object.GetFeaturePrecision(refId);
            Assert.AreEqual(3, precision1);

            List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, statusGraphics);

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, refId, 0 });

            var precision2 = plugin.Object.GetFeaturePrecision(refId);
            Assert.AreEqual(1, precision2);
        }

        [TestMethod]
        public void InitFirstTime()
        {
            TestHelper.CreateMockPlugInAndMoqHsController(out var plugin, out var mockHsController);

            Assert.IsTrue(plugin.Object.InitIO());
            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();

            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterFeaturePage(PlugInData.PlugInId, "dbstats.html", "Database statistics"));
            mockHsController.Verify(x => x.RegisterFeaturePage(PlugInData.PlugInId, "alldevices.html", "Device statistics"));
            mockHsController.Verify(x => x.RegisterDeviceIncPage(PlugInData.PlugInId, "adddevice.html", "Add a statistics device"));

            string dbPath = Path.Combine(mockHsController.Object.GetAppPath(), "data", PlugInData.PlugInId, "records.db");
            Assert.IsTrue(File.Exists(dbPath));
        }

        [TestMethod]
        public void IsFeatureTrackedForTimer()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 9456;
            mockHsController.SetupFeature(refId, 1.132, "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var tracked1 = plugin.Object.IsFeatureTracked(refId);
            Assert.IsTrue(tracked1);
            Assert.IsTrue(plugin.Object.HasJuiDeviceConfigPage(refId));

            var data = new PlugExtraData();
            data.AddNamed("timername", "123");
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.PlugExtraData, data);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.Interface, string.Empty);

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, refId, 0 });

            var tracked2 = plugin.Object.IsFeatureTracked(refId);
            Assert.IsFalse(tracked2);

            Assert.IsFalse(plugin.Object.HasJuiDeviceConfigPage(refId));
        }

        [TestMethod]
        public void PostBackProcforNonHandled()
        {
            var plugin = new PlugIn();
            Assert.AreEqual(plugin.PostBackProc("Random", "data", "user", 0), string.Empty);
        }

        [TestMethod]
        public void VerifyAccessLevel()
        {
            var plugin = new PlugIn();
            Assert.AreEqual((int)EAccessLevel.RequiresLicense, plugin.AccessLevel);
        }

        [TestMethod]
        public void VerifyNameAndId()
        {
            var plugin = new PlugIn();
            Assert.AreEqual(PlugInData.PlugInId, plugin.Id);
            Assert.AreEqual(PlugInData.PlugInName, plugin.Name);
        }

        [TestMethod]
        public void VerifySupportsConfigDeviceAll()
        {
            var plugin = new PlugIn();
            Assert.IsTrue(plugin.SupportsConfigDeviceAll);
            Assert.IsTrue(plugin.SupportsConfigFeature);
            Assert.IsTrue(plugin.SupportsConfigDevice);
        }

        [TestMethod]
        public void UseWithoutInit()
        {
            var plugin = new PlugIn();
            Assert.ThrowsException<InvalidOperationException>(() => plugin.PruneDatabase());
            Assert.ThrowsException<InvalidOperationException>(() => plugin.DeleteAllRecords(10));
            Assert.ThrowsException<InvalidOperationException>(() => plugin.GetDatabaseStats());
        }
    }
}