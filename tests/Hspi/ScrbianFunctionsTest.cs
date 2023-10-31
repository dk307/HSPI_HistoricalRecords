using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi;
using Hspi.Device;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class ScrbianFunctionsTest
    {
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void GetStatisticDeviceDataAsJson(bool isDevice)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 10300;
            int statsFeatureRefId = 10300;
            int trackedDeviceRefId = 100;

            TestHelper.SetupStatisticsDevice(StatisticsFunction.AverageLinear, plugIn, hsControllerMock, aTime,
                                             statsDeviceRefId, trackedDeviceRefId);

            hsControllerMock.SetupDevice(102984);
            hsControllerMock.SetupDevOrFeatureValue(statsDeviceRefId, EProperty.AssociatedDevices, new HashSet<int> { statsDeviceRefId });

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            var data = ((PlugExtraData)hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.PlugExtraData)).GetNamed("data");

            // get return function value for feature
            string json = plugIn.Object.GetStatisticDeviceDataAsJson(isDevice ? statsDeviceRefId : statsFeatureRefId);
            Assert.AreEqual(JsonConvert.DeserializeObject<StatisticsDeviceData>(data),
                            JsonConvert.DeserializeObject<StatisticsDeviceData>(json));
        }

        [TestMethod]
        public void GetAllDevicesProperties()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = DateTime.Now;

            List<int> hsFeatures = new();

            for (int i = 0; i < 15; i++)
            {
                mockHsController.SetupFeature(1307 + i, 1.1, displayString: "1.1", lastChange: nowTime);
                hsFeatures.Add(1307 + i);
            }

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            for (int i = 0; i < 15; i++)
            {
                TestHelper.WaitForRecordCountAndDeleteAll(plugin, hsFeatures[i], 1);
                for (int j = 0; j < i; j++)
                {
                    TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                   hsFeatures[i], i, i.ToString(), nowTime.AddMinutes(i * j), j + 1);
                }
            }

            var stats = plugin.Object.GetAllDevicesProperties();
            Assert.IsNotNull(stats);
            Assert.AreEqual(15, stats.Count);

            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(1307 + i, stats[i]["ref"]);
                Assert.AreEqual((long)i, stats[i]["records"]);
                Assert.AreEqual(true, stats[i]["monitorableType"]);
                Assert.AreEqual(true, stats[i]["tracked"]);
            }
        }

        [TestMethod]
        public void GetAllowedDisplaysForNoRangeFeature()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 1110;
            mockHsController.SetupFeature(refId, 10, "10.0 lux", lastChange: nowTime);

            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusControls, new List<StatusControl>());
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, new List<StatusGraphic>());

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            var list = plugin.Object.GetAllowedDisplays(refId);
            CollectionAssert.AreEqual(new List<string>() { "table" }, list);
        }

        [TestMethod]
        public void GetAllowedDisplaysForFeatureWithRange()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 1110;
            mockHsController.SetupFeature(refId, 10, "10.0 lux", lastChange: nowTime);

            List<StatusGraphic> graphics = new()
            {
                new StatusGraphic("path", new ValueRange(0, 100))
            };
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, graphics);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            var list = plugin.Object.GetAllowedDisplays(refId);
            CollectionAssert.AreEqual(new List<string>() { "table", "chart" }, list);
        }

        [TestMethod]
        public void GetDatabaseStats()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int ref1 = 42;
            int ref2 = 43;

            mockHsController.SetupFeature(ref1, 1.1, displayString: "1.1", lastChange: nowTime);
            mockHsController.SetupFeature(ref2, 1.1, displayString: "4.5", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            for (int i = 0; i < 10; i++)
            {
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         ref1, i, i.ToString(), nowTime.AddMinutes(i), i + 1);
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         ref2, i, i.ToString(), nowTime.AddMinutes(i), i + 1);
            }

            var stats = plugin.Object.GetDatabaseStats();
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.ContainsKey("Path"));
            Assert.IsTrue(stats.ContainsKey("Sqlite version"));
            Assert.IsTrue(stats.ContainsKey("Sqlite memory used"));
            Assert.IsTrue(stats.ContainsKey("Size"));
            Assert.AreEqual("20", stats["Total records"]);
            Assert.AreEqual("20", stats["Total records from last 24 hr"]);
        }

        [TestMethod]
        public void GetDeviceStatsForPage()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 1110;
            mockHsController.SetupFeature(refId, 10, "10.0 lux", lastChange: nowTime);

            List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, statusGraphics);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, refId, 1));

            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                         refId, 11, "11.0 lux", nowTime.AddMinutes(1), 2);

            var list = plugin.Object.GetDeviceStatsForPage(refId).ToList();

            var expected = new List<object>
            {
                0L,
                -60L,
                true,
                1,
                "lux"
            };

            CollectionAssert.AreEqual(expected, list);
        }

        [TestMethod]
        public void GetFeatureRefIdsForDevice()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            mockHsController.SetupDevice(100);
            mockHsController.SetupFeature(103, 0);
            mockHsController.SetupFeature(101, 0);
            mockHsController.SetupFeature(102, 0);

            HashSet<int> value = new() { 103, 101, 102 };
            mockHsController.SetupDevOrFeatureValue(100,
                                                    HomeSeer.PluginSdk.Devices.EProperty.AssociatedDevices,
                                                    value);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var list = plugin.Object.GetFeatureRefIdsForDevice(100).ToList();
            CollectionAssert.AreEqual(value.ToList(), list);
        }
    }
}