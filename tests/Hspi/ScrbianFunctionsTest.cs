﻿using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi;
using Hspi.Device;
using Newtonsoft.Json;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class ScrbianFunctionsTest
    {
        [Test]
        public void GetAllDevicesProperties()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

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
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.Count, Is.EqualTo(15));

            for (int i = 0; i < 15; i++)
            {
                Assert.That(stats[i]["ref"], Is.EqualTo(1307 + i));
                Assert.That(stats[i]["records"], Is.EqualTo((long)i));
                Assert.That(stats[i]["monitorableType"], Is.EqualTo(true));
                Assert.That(stats[i]["tracked"], Is.EqualTo(true));
                Assert.That(stats[i]["minValue"], Is.EqualTo(null));
                Assert.That(stats[i]["maxValue"], Is.EqualTo(null));
            }
        }

        [Test]
        public void GetAllDevicesProperties2()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);
            List<int> hsFeatures = new();

            int refId = 1307;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);
            hsFeatures.Add(refId);

            TestHelper.SetupPerDeviceSettings(mockHsController, refId, false, 10, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var stats = plugin.Object.GetAllDevicesProperties();
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.Count, Is.EqualTo(1));

            Assert.That(stats[0]["ref"], Is.EqualTo(refId));
            Assert.That(stats[0]["records"], Is.EqualTo(0L));
            Assert.That(stats[0]["monitorableType"], Is.EqualTo(true));
            Assert.That(stats[0]["tracked"], Is.EqualTo(false));
            Assert.That(stats[0]["minValue"], Is.EqualTo(10D));
            Assert.That(stats[0]["maxValue"], Is.EqualTo(100D));
        }

        [Test]
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
            CollectionAssert.AreEqual(new List<string>() { "table", "chart", "stats", "histogram" }, list);
        }

        [Test]
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
            CollectionAssert.AreEqual(new List<string>() { "table", "histogram" }, list);
        }

        [Test]
        public void GetDatabaseStats()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var stats = plugin.Object.GetDatabaseStats();
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.ContainsKey("path"));
            Assert.That(stats.ContainsKey("version"));
            Assert.That(stats.ContainsKey("size"));
            Assert.That(stats.ContainsKey("retentionPeriod"));
        }

        [Test]
        public void GetDeviceStatsForPage()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 1110;
            mockHsController.SetupFeature(refId, 10, "10.0 lux", lastChange: nowTime);

            List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.StatusGraphics, statusGraphics);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.That(TestHelper.WaitTillTotalRecords(plugin, refId, 1));

            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                         refId, 11, "11.0 lux", nowTime.AddMinutes(1), 2);

            var list = plugin.Object.GetDevicePageHeaderStats(refId).ToList();

            var expected = new List<object>
            {
                0L,
                -60L,
                true,
                1,
                "lux",
                null,
                null,
            };

            CollectionAssert.AreEqual(expected, list);
        }

        [Test]
        public void GetDeviceStatsForPageReturnsRange()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 11310;
            mockHsController.SetupFeature(refId, 10, "10.0 lux", lastChange: nowTime);

            mockHsController.SetupIniValue("Settings", "DeviceSettings", refId.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "RefId", refId.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "IsTracked", true.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "RetentionPeriod", string.Empty);
            mockHsController.SetupIniValue(refId.ToString(), "MinValue", "-190");
            mockHsController.SetupIniValue(refId.ToString(), "MaxValue", "1090");

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.That(TestHelper.WaitTillTotalRecords(plugin, refId, 1));

            var list = plugin.Object.GetDevicePageHeaderStats(refId).ToList();

            Assert.That(list[5], Is.EqualTo(-190D));
            Assert.That(list[6], Is.EqualTo(1090D));
        }

        [Test]
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

        [TestCase(true)]
        [TestCase(false)]
        public void GetStatisticDeviceDataAsJson(bool isDevice)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 10300;
            int statsFeatureRefId = 10301;
            int trackedDeviceRefId = 100;

            hsControllerMock.SetupDevice(statsDeviceRefId, deviceInterface: PlugInData.PlugInId);
            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageLinear, plugIn, hsControllerMock, aTime,
                                             statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            var data = ((PlugExtraData)hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.PlugExtraData)).GetNamed("data");

            // get return function value for feature
            string json = plugIn.Object.GetStatisticDeviceDataAsJson(isDevice ? statsDeviceRefId : statsFeatureRefId);
            Assert.That(JsonConvert.DeserializeObject<StatisticsDeviceData>(data),
                        Is.EqualTo(JsonConvert.DeserializeObject<StatisticsDeviceData>(json)));
        }

        [Test]
        public void GetTrackedDeviceList()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            List<int> hsFeatures = new();

            for (int i = 0; i < 15; i++)
            {
                mockHsController.SetupFeature(1307 + i, 1.1, displayString: "1.1", lastChange: nowTime);
                hsFeatures.Add(1307 + i);
            }

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var stats = plugin.Object.GetTrackedDeviceList();

            CollectionAssert.AreEqual(hsFeatures, stats);
        }
    }
}