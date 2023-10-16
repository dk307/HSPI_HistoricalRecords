using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                                          35673,
                                                          1.132,
                                                          displayString: displayStatus,
                                                          lastChange: DateTime.Now - TimeSpan.FromDays(6));

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(plugin, mockHsController, feature, eventType);

            RecordData recordData = new(feature.Ref, feature.Value,
                                                     expectedString,
                                                     ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
            TestHelper.CheckRecordedValue(plugin, feature.Ref, recordData, 100, 1);

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

            TestHelper.RaiseHSEvent(plugin, mockHsController, feature, Constants.HSEvent.VALUE_CHANGE);
            expected.Add(new RecordData(feature.Ref, feature.Value, feature.DisplayedStatus, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds()));

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            AddNewChange(expected, 1.2, time.AddSeconds(1));
            AddNewChange(expected, 1.3, time.AddSeconds(2));
            AddNewChange(expected, 1.3, time.AddSeconds(3));
            AddNewChange(expected, 1.8, time.AddSeconds(4));
            AddNewChange(expected, 11.8, time.AddSeconds(5));

            var records = TestHelper.GetHistoryRecords(plugin, feature.Ref, 100);
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

            TestHelper.RaiseHSEvent(plugin, mockHsController, feature, Constants.HSEvent.VALUE_CHANGE);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            feature.Changes[EProperty.Value] = 834D;
            feature.Changes[EProperty.DisplayedStatus] = "34324";
            // No time change is done here

            TestHelper.RaiseHSEvent(plugin, mockHsController, feature, Constants.HSEvent.VALUE_CHANGE);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                var records = TestHelper.GetHistoryRecords(plugin, feature.Ref, 100);
                Assert.IsNotNull(records);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.AreEqual(1, records.Count);

                return (feature.Ref == records[0].DeviceRefId) &&
                       (feature.Value == records[0].DeviceValue) &&
                       (feature.DisplayedStatus == records[0].DeviceString) &&
                       (((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds() == records[0].UnixTimeSeconds);
            }));

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("timername")]
        [DataRow("countername")]
        public void TimerOrCounterChangeIsNotRecorded(string plugInExtraKey)
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController, 673, 1);

            var data = new PlugExtraData();
            data.AddNamed(plugInExtraKey, "123");
            mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, EProperty.PlugExtraData)).Returns(data);
            mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, EProperty.Interface)).Returns(string.Empty);

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(plugin, mockHsController, feature, Constants.HSEvent.VALUE_CHANGE);

            var records = TestHelper.GetHistoryRecords(plugin, feature.Ref);
            Assert.IsTrue(records.Count == 0);

            // this is not a good test as maynot actually end up waiting for failure

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void UnTrackedDeviceIsNotStored()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime time = DateTime.Now;

            int deviceRefId = 35673;
            var feature = TestHelper.SetupHsFeature(mockHsController,
                                     deviceRefId,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            Assert.IsTrue(plugin.Object.InitIO());

            mockHsController.Setup(x => x.SaveINISetting(deviceRefId.ToString(), It.IsAny<string>(), It.IsAny<string>(), PlugInData.SettingFileName));
            plugin.Object.PostBackProc("updatedevicesettings", "{\"refId\":\"35673\",\"tracked\":0}", string.Empty, 0);

            Assert.IsFalse(plugin.Object.IsDeviceTracked(deviceRefId.ToString()));

            for (var i = 0; i < 100; i++)
            {
                TestHelper.RaiseHSEvent(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE, feature, i, "33", feature.LastChange.AddSeconds(7));
            }

            // this is not a good test as there is no good event to wait to ensure nothing was recorded

            Assert.AreEqual(0, plugin.Object.GetTotalRecords(deviceRefId));

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }
    }
}