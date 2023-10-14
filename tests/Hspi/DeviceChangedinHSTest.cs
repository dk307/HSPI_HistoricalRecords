﻿using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class DeviceChangedinHSTest
    {
        [DataTestMethod]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, "abcd", "abcd")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, "abcd", "abcd")]
        [TestMethod]
        public void DeviceValueUpdateIsRecorded(Constants.HSEvent eventType, string displayStatus, string expectedString)
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                                          35673,
                                                          1.132,
                                                          displayString: displayStatus,
                                                          lastChange: DateTime.Now - TimeSpan.FromDays(6));

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(eventType, plugin, mockHsController, feature);

            RecordData recordData = new(feature.Ref, feature.Value,
                                                     expectedString,
                                                     ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
            TestHelper.CheckRecordedValue(plugin, feature.Ref, recordData, 100, 1);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void TimerChangeIsNotRecorded()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController, 673, 1);

            feature.Changes[EProperty.DeviceType] = new HomeSeer.PluginSdk.Devices.Identification.TypeInfo()
            {
                ApiType = EApiType.Device,
                Summary = "Timer"
            };

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, mockHsController, feature);

            var records = TestHelper.GetHistoryRecords(plugin, feature.Ref);
            Assert.IsTrue(records.Count == 0);
            // this is not a good test as maynot actually end up waiting for failure

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void MultipleDeviceValueUpdatesAreRecorded()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime time = DateTime.Now;

            var feature = TestHelper.SetupHsFeature(mockHsController,
                                     35673,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            Assert.IsTrue(plugin.Object.InitIO());
            List<RecordData> expected = new();

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, mockHsController, feature);
            expected.Add(new RecordData(feature.Ref, feature.Value, feature.DisplayedStatus, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds()));

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            AddNewChange(expected, 1.2, time.AddSeconds(1));
            AddNewChange(expected, 1.3, time.AddSeconds(2));
            AddNewChange(expected, 1.3, time.AddSeconds(3));
            AddNewChange(expected, 1.8, time.AddSeconds(4));
            AddNewChange(expected, 11.8, time.AddSeconds(5));

            var records = TestHelper.GetHistoryRecords(plugin, feature.Ref, 100);
            records.Reverse();
            CollectionAssert.AreEqual(expected, records);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();

            void AddNewChange(IList<RecordData> expected, double value, DateTime lastChange)
            {
                var added = TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE, feature, value, value.ToString(), lastChange, expected.Count + 1);
                expected.Add(added);
            }
        }

        [TestMethod]
        public void SameSecondChangesAreOverwritten()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime time = DateTime.Now;

            var feature = TestHelper.SetupHsFeature(mockHsController,
                                     35673,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            Assert.IsTrue(plugin.Object.InitIO());
            List<RecordData> expected = new();

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, mockHsController, feature);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            feature.Changes[EProperty.Value] = 834D;
            feature.Changes[EProperty.DisplayedStatus] = "34324";
            // No time change is done here

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, mockHsController, feature);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                var records = TestHelper.GetHistoryRecords(plugin, feature.Ref, 100);
                Assert.IsNotNull(records);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.AreEqual(records.Count, 1);

                return (feature.Ref == records[0].DeviceRefId) &&
                       (feature.Value == records[0].DeviceValue) &&
                       (feature.DisplayedStatus == records[0].DeviceString) &&
                       (((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds() == records[0].UnixTimeSeconds);
            }));

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }
    }
}