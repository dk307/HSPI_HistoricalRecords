using System;
using System.Collections.Generic;
using System.IO;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            List<int> allDeviceRefs = new List<int> { 1000, 1001 };
            mockHsController.Setup(x => x.GetAllRefs()).Returns(allDeviceRefs);
            var feature1 = SetupHsFeature(mockHsController, allDeviceRefs[0], 1.1, DateTime.Now - TimeSpan.FromDays(6));
            var feature2 = SetupHsFeature(mockHsController, allDeviceRefs[1], 2221.1, DateTime.Now - TimeSpan.FromDays(24));

            Assert.IsTrue(plugin.Object.InitIO());

            //wait till task finishes
            CheckRecordedValue(plugin, allDeviceRefs[0], feature1);
            CheckRecordedValue(plugin, allDeviceRefs[1], feature2);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
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

        private static void CheckRecordedValue(Mock<PlugIn> plugin, int deviceRefId, HsFeature feature)
        {
            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                var records = GetRecords(plugin, deviceRefId);
                Assert.IsNotNull(records);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.AreEqual(records.Count, 1);
                Assert.AreEqual(records[0].DeviceValue, feature.Value);
                Assert.AreEqual(records[0].UnixTimeSeconds, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
                Assert.AreEqual(records[0].DeviceString, feature.StatusString);

                return true;
            }));
        }

        private static IList<RecordData> GetRecords(Mock<PlugIn> plugin, int refId, int recordLimit = 10)
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

        private HsFeature SetupHsFeature(Mock<IHsController> mockHsController, int deviceRefId,
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

        private HsFeature SetupHsFeature(Mock<IHsController> mockHsController,
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