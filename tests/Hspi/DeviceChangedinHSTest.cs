using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using NUnit.Framework;
using static HomeSeer.PluginSdk.PluginStatus;

namespace HSPI_HistoryTest

{
    [TestFixture]
    public class DeviceChangedinHSTest
    {
        [TestCase(Constants.HSEvent.VALUE_CHANGE, "abcd", "abcd")]
        [TestCase(Constants.HSEvent.STRING_CHANGE, "abcd3", "abcd3")]
        public void DeviceValueUpdateIsRecorded(Constants.HSEvent eventType, string displayStatus, string expectedString)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 35673;
            mockHsController.SetupFeature(refId, 1.132, displayString: displayStatus,
                                                        lastChange: DateTime.Now - TimeSpan.FromDays(6));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, refId, 1);

            var now = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);
            var data = TestHelper.RaiseHSEventAndWait(plugin, mockHsController, eventType, refId,
                                                      100, displayStatus, now, 1);

            Assert.That(data.DeviceValue, Is.EqualTo(100));
            Assert.That(data.DeviceString, Is.EqualTo(expectedString));
            Assert.That(data.UnixTimeSeconds, Is.EqualTo(((DateTimeOffset)now).ToUnixTimeSeconds()));
        }

        [Test]
        public void InvalidLastChangeTimeIsNotRecorded()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 35673;
            mockHsController.SetupFeature(refId, 1.132);

            var now = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, refId, 1);

            //raise out of range value event
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.Value, 100D);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.LastChange, DateTime.MinValue);

            TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.STRING_CHANGE, refId);

            // raise a normal range event
            var data = TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.STRING_CHANGE, refId,
                                          10, string.Empty, now, 1);

            Assert.That(data.DeviceValue, Is.EqualTo(10));
            Assert.That(plugin.Object.GetTotalRecords(refId), Is.EqualTo(1));
        }

        [Test]
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

            Assert.That(records.Count, Is.EqualTo(expected.Count));

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var expectedRecord = expected[i];

                Assert.That(expectedRecord.DeviceRefId, Is.EqualTo(record.DeviceRefId));
                Assert.That(expectedRecord.UnixTimeSeconds, Is.EqualTo(record.UnixTimeSeconds));
                Assert.That(expectedRecord.DeviceValue, Is.EqualTo(record.DeviceValue));
                Assert.That(expectedRecord.DeviceString, Is.EqualTo(record.DeviceString));
            }
        }

        [TestCase(-10)]
        [TestCase(104)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void OutOfRangeDeviceValueIsNotRecorded(double value)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 35673;
            TestHelper.SetupPerDeviceSettings(mockHsController, refId, true, 0, 100);

            mockHsController.SetupFeature(refId, 1.132);

            var now = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, refId, 1);

            //raise out of range value event
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.Value, value);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.LastChange, now.AddHours(1));

            TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.STRING_CHANGE, refId);

            // raise a normal range event
            var data = TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.STRING_CHANGE, refId,
                                          10, string.Empty, now, 1);

            Assert.That(data.DeviceValue, Is.EqualTo(10));
            Assert.That(plugin.Object.GetTotalRecords(refId), Is.EqualTo(1));
        }

        [Test]
        public void RecordALargeNumberOfEvents()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);

            DateTime time = DateTime.Now;

            int deviceRefId = 35673;
            mockHsController.SetupFeature(deviceRefId,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, deviceRefId, 1);

            for (var i = 0; i < 1024; i++)
            {
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                        deviceRefId, i, "33", time.AddSeconds(i * 5), i + 1);
            }
        }

        [Test]
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

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
            {
                var records = TestHelper.GetHistoryRecords(plugin, refId, 100);
                Assert.That(records, Is.Not.Null);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.That(records.Count, Is.EqualTo(1));

                return (refId == records[0].DeviceRefId) &&
                       (2.34 == records[0].DeviceValue) &&
                       ("2.34" == records[0].DeviceString) &&
                       (((DateTimeOffset)time).ToUnixTimeSeconds() == records[0].UnixTimeSeconds);
            }));
        }

        [TestCase("timername")]
        [TestCase("countername")]
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
            Assert.That(records.Count == 0);

            // this is not a good test as maynot actually end up waiting for failure
        }

        [Test]
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

            Assert.That(!plugin.Object.IsFeatureTracked(deviceRefId));

            for (var i = 0; i < 100; i++)
            {
                TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.VALUE_CHANGE, deviceRefId);
            }

            // this is not a good test as there is no good event to wait to ensure nothing was recorded

            Assert.That(plugin.Object.GetTotalRecords(deviceRefId), Is.EqualTo(0));
        }

        [Test]
        public void UpdateRecordFails()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int refId = 35673;
            mockHsController.SetupFeature(refId, 10);

            var now = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, refId, 1);

            // make db readonly
            plugin.Object.ExecSql(@"PRAGMA query_only = true");

            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE, refId,
                                           100, string.Empty, now, 1);

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Critical == plugin.Object.OnStatusCheck().Status;
            }));

            Assert.That(plugin.Object.OnStatusCheck().StatusText, Is.EqualTo("attempt to write a readonly database"));

            Assert.That(plugin.Object.GetTotalRecords(refId), Is.EqualTo(1));
        }
    }
}