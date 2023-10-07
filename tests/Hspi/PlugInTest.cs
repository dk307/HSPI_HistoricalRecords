using System;
using System.Collections.Generic;
using System.IO;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class PlugInTest
    {
        [TestMethod]
        public void AllDevicesAreUpdatedOnStart()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            List<int> allDeviceRefs = new() { 1000, 1001 };
            mockHsController.Setup(x => x.GetAllRefs()).Returns(allDeviceRefs);
            var feature1 = SetupHsFeature(mockHsController, allDeviceRefs[0], 1.1, DateTime.Now - TimeSpan.FromDays(6));
            var feature2 = SetupHsFeature(mockHsController, allDeviceRefs[1], 2221.1, DateTime.Now - TimeSpan.FromDays(24));

            Assert.IsTrue(plugin.Object.InitIO());

            //wait till task finishes
            CheckRecordedValue(plugin, allDeviceRefs[0], feature1, 100, 1);
            CheckRecordedValue(plugin, allDeviceRefs[1], feature2, 100, 1);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
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
        public void InitFirstTime()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            Assert.IsTrue(plugin.Object.InitIO());
            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();

            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, PlugInData.PlugInId));
            mockHsController.Verify(x => x.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, PlugInData.PlugInId));

            string dbPath = Path.Combine(mockHsController.Object.GetAppPath(), "data", PlugInData.PlugInId, "records.db");
            Assert.IsTrue(File.Exists(dbPath));
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

        private static void CheckRecordedValue(Mock<PlugIn> plugin, int deviceRefId, HsFeature feature,
                                               int askForRecordCount, int expectedRecordCount)
        {
            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                var records = GetHistoryRecords(plugin, deviceRefId, askForRecordCount);
                Assert.IsNotNull(records);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.AreEqual(records.Count, expectedRecordCount);
                Assert.IsTrue(records.Count >= 1);
                Assert.AreEqual(records[0].DeviceValue, feature.Value);
                Assert.AreEqual(records[0].UnixTimeSeconds, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
                Assert.AreEqual(records[0].DeviceString, feature.StatusString);

                return true;
            }));
        }

        private static IList<RecordData> GetHistoryRecords(Mock<PlugIn> plugin, int refId, int recordLimit = 10)
        {
            List<RecordData> result = new();
            string data = plugin.Object.PostBackProc("historyrecords", $"refId={refId}&recordLimit={recordLimit}&start=0&length={recordLimit}", string.Empty, 0);
            if (!string.IsNullOrEmpty(data))
            {
                var jsonData = (JObject)JsonConvert.DeserializeObject(data);
                Assert.IsNotNull(jsonData);
                var records = (JArray)jsonData["data"];
                Assert.IsNotNull(records);

                foreach (var record in records)
                {
                    var recordArray = (JArray)record;
                    result.Add(new RecordData(refId,
                                              recordArray[1].Value<double>(),
                                              recordArray[2].Value<string>(),
                                              recordArray[0].Value<long>() / 1000));
                }
            }

            return result;
        }

        private static HsFeature SetupHsFeature(Mock<IHsController> mockHsController, int deviceRefId,
                                                                            IDictionary<EProperty, object> changes)
        {
            HsFeature feature = new HsFeature(deviceRefId);
            foreach (var change in changes)
            {
                feature.Changes.Add(change.Key, change.Value);
            }

            mockHsController.Setup(x => x.GetFeatureByRef(deviceRefId)).Returns(feature);
            return feature;
        }

        private static HsFeature SetupHsFeature(Mock<IHsController> mockHsController,
                                    int deviceRefId,
                                    double value,
                                    DateTime? lastChange = null,
                                    string featureInterface = null,
                                    int deviceType = (int)HomeSeer.PluginSdk.Devices.Identification.EApiType.Device)
        {
            return SetupHsFeature(mockHsController, deviceRefId, new Dictionary<EProperty, object>() {
                    { EProperty.Interface, featureInterface },
                    { EProperty.Value, value },
                    { EProperty.LastChange, lastChange ?? DateTime.Now },
                    { EProperty.DeviceType, new HomeSeer.PluginSdk.Devices.Identification.TypeInfo() { Type =  deviceType} },
                });
        }
    }
}