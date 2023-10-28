using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class DeviceChangedinHSTest
    {
        [DataTestMethod]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, "abcd", "abcd")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, "abcd3", "abcd3")]
        [TestMethod]
        public void DeviceValueUpdateIsRecorded(Constants.HSEvent eventType, string displayStatus, string expectedString)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 35673;
            mockHsController.SetupFeature(refId, 1.132, displayString: displayStatus,
                                                        lastChange: DateTime.Now - TimeSpan.FromDays(6));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, refId, 1);

            DateTime now = DateTime.Now;
            var data = TestHelper.RaiseHSEventAndWait(plugin, mockHsController, eventType, refId,
                                                      100, displayStatus, now, 1);

            Assert.AreEqual(100, data.DeviceValue);
            Assert.AreEqual(expectedString, data.DeviceString);
            Assert.AreEqual(((DateTimeOffset)now).ToUnixTimeSeconds(), data.UnixTimeSeconds);
        }

        [TestMethod]
        public void MultipleDeviceValueUpdatesAreRecorded()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime time = DateTime.Now;

            int refId = 35673;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: time);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, refId, 1);

            List<RecordData> expected = new()
            {
                new RecordData(refId, 1.1, "1.1", ((DateTimeOffset)time).ToUnixTimeSeconds())
            };

            for (int i = 0; i < 5; i++)
            {
                expected.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE, refId,
                                               1.0 + 0.1 * i, "", time.AddSeconds(i + 1), expected.Count + 1));
            }

            var records = TestHelper.GetHistoryRecords(plugin, refId, 100);
            records.Reverse();

            Assert.AreEqual(expected.Count, records.Count);

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var expectedRecord = expected[i];

                Assert.AreEqual(record.DeviceRefId, expectedRecord.DeviceRefId);
                Assert.AreEqual(record.UnixTimeSeconds, expectedRecord.UnixTimeSeconds);
                Assert.AreEqual(record.DeviceValue, expectedRecord.DeviceValue);
                Assert.AreEqual(record.DeviceString, expectedRecord.DeviceString);
            }
        }

        [TestMethod]
        public void SameSecondChangesAreOverwritten()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime time = DateTime.Now;

            int refId = 35673;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: time);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, refId, 1);

            mockHsController.SetupDevOrFeatureValue(refId, EProperty.Value, 2.34);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.DisplayedStatus, "2.34");
            // No time change is done here

            TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.VALUE_SET, refId);

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                var records = TestHelper.GetHistoryRecords(plugin, refId, 100);
                Assert.IsNotNull(records);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.AreEqual(1, records.Count);

                return (refId == records[0].DeviceRefId) &&
                       (2.34 == records[0].DeviceValue) &&
                       ("2.34" == records[0].DeviceString) &&
                       (((DateTimeOffset)time).ToUnixTimeSeconds() == records[0].UnixTimeSeconds);
            }));
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("timername")]
        [DataRow("countername")]
        public void TimerOrCounterChangeIsNotRecorded(string plugInExtraKey)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 673;
            mockHsController.SetupFeature(673, 1);

            var data = new PlugExtraData();
            data.AddNamed(plugInExtraKey, "123");
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.PlugExtraData, data);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.Interface, string.Empty);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.VALUE_CHANGE, refId);

            var records = TestHelper.GetHistoryRecords(plugin, refId);
            Assert.IsTrue(records.Count == 0);

            // this is not a good test as maynot actually end up waiting for failure
        }

        [TestMethod]
        public void UnTrackedDeviceIsNotStored()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime time = DateTime.Now;

            int deviceRefId = 35673;
            mockHsController.SetupFeature(deviceRefId,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, deviceRefId, 1);

            // disable tracking
            plugin.Object.PostBackProc("updatedevicesettings", $"{{\"refId\":\"{deviceRefId}\",\"tracked\":0}}", string.Empty, 0);

            Assert.IsFalse(plugin.Object.IsFeatureTracked(deviceRefId));

            for (var i = 0; i < 100; i++)
            {
                TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.VALUE_CHANGE, deviceRefId);
            }

            // this is not a good test as there is no good event to wait to ensure nothing was recorded

            Assert.AreEqual(0, plugin.Object.GetTotalRecords(deviceRefId));
        }
    }
}