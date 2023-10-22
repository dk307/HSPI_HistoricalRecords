﻿using System;
using System.Collections.Generic;
using System.IO;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
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
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);
            List<int> allDeviceRefs = new() { 1000, 1001 };
            mockHsController.Setup(x => x.GetAllRefs()).Returns(allDeviceRefs);
            var feature1 = TestHelper.SetupHsFeature(mockHsController, allDeviceRefs[0], 1.1, "abcd", lastChange: DateTime.Now - TimeSpan.FromDays(6));
            var feature2 = TestHelper.SetupHsFeature(mockHsController, allDeviceRefs[1], 2221.1, "rteyee", lastChange: DateTime.Now - TimeSpan.FromDays(24));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.CheckRecordedValueForFeatureType(plugin, feature1, 100, 1);
            TestHelper.CheckRecordedValueForFeatureType(plugin, feature2, 100, 1);
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

            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController(settingsFromIni);

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
            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController(new Dictionary<string, string>());

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

            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController(settingsFromIni);

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
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                              35673,
                                              1.132,
                                              "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var unit1 = plugin.Object.GetFeatureUnit(feature.Ref);
            Assert.AreEqual("F", unit1);

            mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, EProperty.DisplayedStatus)).Returns("1.1 C");

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, feature.Ref });

            var unit2 = plugin.Object.GetFeatureUnit(feature.Ref);
            Assert.AreEqual("C", unit2);
        }

        [TestMethod]
        public void GetJuiDeviceConfigPageForDevice()
        {
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);
            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devOrFeatRef = 10;

            mockHsController.Setup(x => x.IsRefDevice(devOrFeatRef)).Returns(true);

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(devOrFeatRef);

            var data = (JObject)JsonConvert.DeserializeObject(pageJson);
            Assert.IsNotNull(data);
            Assert.AreEqual(PlugInData.PlugInId, data["id"].Value<string>());
            Assert.AreEqual("Device", data["name"].Value<string>());
            Assert.AreEqual(5, data["type"].Value<int>());

            string labelHtml = data["views"][0]["name"].Value<string>();

            var htmlDoc = TestHelper.VerifyHtmlValid(labelHtml);
            var iFrameElement = htmlDoc.GetElementbyId("historicalrecordsiframeid");

            var iFrameSource = iFrameElement.Attributes["src"].Value;
            Assert.AreEqual(iFrameSource, $"/HistoricalRecords/devicehistoricalrecords.html?ref={devOrFeatRef}&feature={devOrFeatRef}");
        }

        [TestMethod]
        public void GetJuiDeviceConfigPageForFeature()
        {
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);
            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devOrFeatRef = 10;

            mockHsController.Setup(x => x.IsRefDevice(devOrFeatRef)).Returns(false);
            mockHsController.Setup(x => x.GetPropertyByRef(devOrFeatRef, EProperty.AssociatedDevices)).Returns(new HashSet<int>() { 9 });

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(devOrFeatRef);

            var data = (JObject)JsonConvert.DeserializeObject(pageJson);
            Assert.IsNotNull(data);
            Assert.AreEqual(PlugInData.PlugInId, data["id"].Value<string>());
            Assert.AreEqual("Device", data["name"].Value<string>());
            Assert.AreEqual(5, data["type"].Value<int>());

            string labelHtml = data["views"][0]["name"].Value<string>();

            var htmlDoc = TestHelper.VerifyHtmlValid(labelHtml);
            var iFrameElement = htmlDoc.GetElementbyId("historicalrecordsiframeid");

            var iFrameSource = iFrameElement.Attributes["src"].Value;
            Assert.AreEqual(iFrameSource, $"/HistoricalRecords/devicehistoricalrecords.html?ref={9}&feature={devOrFeatRef}");
        }

        [TestMethod]
        public void GetJuiDeviceConfigPageErrored()
        {
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int deviceRefId = 10;

            string errorMessage = "sdfsd dfgdfg erter";
            mockHsController.Setup(x => x.GetFeatureByRef(deviceRefId)).Throws(new Exception(errorMessage));

            string pageJson = plugin.Object.GetJuiDeviceConfigPage(deviceRefId);

            var result = (JObject)JsonConvert.DeserializeObject(pageJson);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result["views"][0]["value"], errorMessage);
        }

        [TestMethod]
        public void GetPrecision()
        {
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                              35673,
                                              1.132,
                                              "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var precision1 = plugin.Object.GetFeaturePrecision(feature.Ref);
            Assert.AreEqual(3, precision1);

            List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, EProperty.StatusGraphics)).Returns(statusGraphics);

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, feature.Ref });

            var precision2 = plugin.Object.GetFeaturePrecision(feature.Ref);
            Assert.AreEqual(1, precision2);
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
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            mockHsController.Setup(x => x.GetPropertyByRef(100, EProperty.DisplayedStatus)).Returns(displayStatus);

            var unitFound = plugin.Object.GetFeatureUnit(100);
            Assert.AreEqual(unit, unitFound);
        }

        [TestMethod]
        public void InitFirstTime()
        {
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);

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
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                              35673,
                                              1.132,
                                              "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var tracked1 = plugin.Object.IsFeatureTracked(feature.Ref);
            Assert.IsTrue(tracked1);
            Assert.IsTrue(plugin.Object.HasJuiDeviceConfigPage(feature.Ref));

            var data = new PlugExtraData();
            data.AddNamed("timername", "123");
            mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, EProperty.PlugExtraData)).Returns(data);
            mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, EProperty.Interface)).Returns(string.Empty);

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, feature.Ref });

            var tracked2 = plugin.Object.IsFeatureTracked(feature.Ref);
            Assert.IsFalse(tracked2);

            Assert.IsFalse(plugin.Object.HasJuiDeviceConfigPage(feature.Ref));
        }

        [TestMethod]
        public void IsFeatureTrackedForThisPluginDevices()
        {
            TestHelper.CreateMockPlugInAndHsController(out var plugin, out var mockHsController);

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                              35673,
                                              1.132,
                                              "1.1 F");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var tracked1 = plugin.Object.IsFeatureTracked(feature.Ref);
            Assert.IsTrue(tracked1);
            Assert.IsTrue(plugin.Object.HasJuiDeviceConfigPage(feature.Ref));

            // invalidate the cache
            plugin.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE, new object[] { 0, 0, 0, feature.Ref });

            mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, EProperty.DeviceType))
                            .Returns(new TypeInfo() { ApiType = EApiType.Device });

            var tracked2 = plugin.Object.IsFeatureTracked(feature.Ref);
            Assert.IsFalse(tracked2);

            Assert.IsFalse(plugin.Object.HasJuiDeviceConfigPage(feature.Ref));
        }

        [TestMethod]
        public void PostBackProcforNonHandled()
        {
            var plugin = new PlugIn();
            Assert.AreEqual(plugin.PostBackProc("Random", "data", "user", 0), string.Empty);
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
        }
    }
}