﻿using System;
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

            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController2(settingsFromIni);

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
            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController2(new Dictionary<string, string>());

            PlugIn plugIn = plugInMock.Object;
            Assert.AreEqual(plugIn.OnStatusCheck().Status, PluginStatus.Ok().Status);
        }

        [TestMethod]
        public void CheckSettingsWithIniFilledDuringInitialize()
        {
            var settingsFromIni = new Dictionary<string, string>()
            {
                { SettingsPages.LoggingLevelId, ((int)LogEventLevel.Information).ToString()},
                { SettingsPages.LogToFileId, true.ToString()},
            };

            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController2(settingsFromIni);

            PlugIn plugIn = plugInMock.Object;

            Assert.IsTrue(plugIn.HasSettings);

            var settingPages = SettingsCollection.FromJsonString(plugIn.GetJuiSettingsPages());
            Assert.IsNotNull(settingPages);

            var settings = settingPages[SettingsPages.SettingPageId].ToValueMap();

            Assert.AreEqual(settings[SettingsPages.LoggingLevelId], ((int)LogEventLevel.Information).ToString());
            Assert.AreEqual(settings[SettingsPages.LogToFileId], true.ToString());
        }

        [TestMethod]
        public void ExecSqlCount()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            List<int> hsFeatures = new();
            for (int i = 0; i < 10; i++)
            {
                mockHsController.SetupFeature(1307 + i, 1.1, displayString: "1.1", lastChange: nowTime);
                hsFeatures.Add(1307 + i);
            }

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            for (int i = 0; i < hsFeatures.Count; i++)
            {
                TestHelper.WaitForRecordCountAndDeleteAll(plugin, hsFeatures[i], 1);
                for (int j = 0; j < i; j++)
                {
                    TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                   hsFeatures[i], i, i.ToString(), nowTime.AddMinutes(i * j), j + 1);
                }
            }

            var jsonString = plugin.Object.PostBackProc("execsql", @"{sql: 'SELECT COUNT(*) AS TotalCount FROM history'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.IsNotNull(json);

            var columns = json["result"]["columns"] as JArray;
            Assert.IsNotNull(columns);
            Assert.AreEqual(1, columns.Count);
            Assert.AreEqual("TotalCount", columns[0].ToString());

            var data = json["result"]["data"] as JArray;
            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual((long)45, data[0][0]);
        }

        [TestMethod]
        public void ExecSqlSingleFeatureAllValues()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 100;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, refId, 1);
            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                           refId, 10, 10.ToString(), nowTime.AddSeconds(1), 2);

            var jsonString = plugin.Object.PostBackProc("execsql", @"{sql: 'SELECT ref, value, str FROM history'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.IsNotNull(json);

            var columns = json["result"]["columns"] as JArray;
            Assert.IsNotNull(columns);
            Assert.AreEqual(3, columns.Count);
            Assert.AreEqual("ref", columns[0].ToString());
            Assert.AreEqual("value", columns[1].ToString());
            Assert.AreEqual("str", columns[2].ToString());

            var data = json["result"]["data"] as JArray;
            Assert.IsNotNull(data);
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual((long)refId, data[0][0]);
            Assert.AreEqual(1.1D, data[0][1]);
            Assert.AreEqual("1.1", data[0][2]);
            Assert.AreEqual((long)refId, data[1][0]);
            Assert.AreEqual(10D, data[1][1]);
            Assert.AreEqual("10", data[1][2]);
        }

        [TestMethod]
        public void ExecSqlVacuum()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 100;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, refId, 1);
            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                           refId, 10, 10.ToString(), nowTime.AddSeconds(1), 2);

            var jsonString = plugin.Object.PostBackProc("execsql", "{sql: 'VACUUM'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.IsNotNull(json);

            var columns = json["result"]["columns"] as JArray;
            Assert.IsNotNull(columns);
            Assert.AreEqual(0, columns.Count);

            var data = json["result"]["data"] as JArray;
            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.Count);
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
    }
}