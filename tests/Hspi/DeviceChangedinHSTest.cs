using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class DeviceChangedinHSTest
    {
        [DataTestMethod]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, EApiType.Feature, "abcd", null, "abcd")]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, EApiType.Feature, null, "1abcde", "1abcde")]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, EApiType.Feature, null, null, null)]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, EApiType.Device, "abcd", null, "abcd")]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, EApiType.Device, "", "abcd", "abcd")]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, EApiType.NotSpecified, "", "1235", "1235")]
        [DataRow(Constants.HSEvent.VALUE_CHANGE, 34, "", "12353", "12353")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, EApiType.Feature, "abcd", null, "abcd")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, EApiType.Feature, null, "1abcde", "1abcde")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, EApiType.Feature, null, null, null)]
        [DataRow(Constants.HSEvent.STRING_CHANGE, EApiType.Device, "1abcd", null, "1abcd")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, EApiType.Device, "", "abcd", "abcd")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, EApiType.NotSpecified, "", "1235", "1235")]
        [DataRow(Constants.HSEvent.STRING_CHANGE, 34, "", "1235", "1235")]
        [TestMethod]
        public void DeviceValueUpdateIsRecorded(Constants.HSEvent eventType, int apiType, string displayStatus, string statusStatus, string expectedString)
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                     35673,
                                     1.132,
                                     displayString: displayStatus,
                                     statusString: statusStatus,
                                     lastChange: DateTime.Now - TimeSpan.FromDays(6),
                                     apiType: apiType);

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(eventType, plugin, feature);

            RecordData recordData = new(feature.Ref, feature.Value,
                                                   expectedString,
                                                   ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
            TestHelper.CheckRecordedValue(plugin, feature.Ref, recordData, 100, 1);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [DataTestMethod]
        [DataRow(Constants.HSEvent.VALUE_CHANGE)]
        [DataRow(Constants.HSEvent.STRING_CHANGE)]
        [TestMethod]
        public void HS3DeviceValueUpdateIsRecordedFromGraphicControls(Constants.HSEvent eventType)
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                     35673,
                                     100,
                                     displayString: null,
                                     statusString: null,
                                     lastChange: DateTime.Now - TimeSpan.FromDays(36),
                                     apiType: (int)EApiType.NotSpecified);

            var controls = new StatusControlCollection();
            controls.Add(new StatusControl(EControlType.ValueRangeSlider) { IsRange = true, TargetRange = new ValueRange(0, 1000) });

            Assert.IsTrue(controls.ContainsValue(feature.Value));

            feature.Changes.Add(EProperty.StatusControls, controls);

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(eventType, plugin, feature);

            RecordData recordData = new(feature.Ref, feature.Value,
                                                   "100",
                                                   ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
            TestHelper.CheckRecordedValue(plugin, feature.Ref, recordData, 100, 1);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [DataTestMethod]
        [DataRow(Constants.HSEvent.VALUE_CHANGE)]
        [DataRow(Constants.HSEvent.STRING_CHANGE)]
        [TestMethod]
        public void HS3DeviceValueUpdateIsRecordedFromStatusGraphic(Constants.HSEvent eventType)
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature = TestHelper.SetupHsFeature(mockHsController,
                                     35673,
                                     100,
                                     displayString: null,
                                     statusString: null,
                                     lastChange: DateTime.Now - TimeSpan.FromDays(36),
                                     apiType: (int)EApiType.NotSpecified);

            var controls = new StatusGraphicCollection();
            controls.Add(new StatusGraphic("path", 0, "Off"));
            controls.Add(new StatusGraphic("path", 100, "On"));

            Assert.IsTrue(controls.ContainsValue(feature.Value));

            feature.Changes.Add(EProperty.StatusGraphics, controls);

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(eventType, plugin, feature);

            RecordData recordData = new(feature.Ref, feature.Value,
                                                   "On",
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

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, feature);

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
                                     lastChange: time,
                                     apiType: (int)EApiType.Feature);

            Assert.IsTrue(plugin.Object.InitIO());
            List<RecordData> expected = new();

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, feature);
            expected.Add(new RecordData(feature.Ref, feature.Value, feature.DisplayedStatus, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds()));

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            AddNewChange(plugin, feature, expected, 1.2, time.AddSeconds(1));
            AddNewChange(plugin, feature, expected, 1.3, time.AddSeconds(2));
            AddNewChange(plugin, feature, expected, 1.3, time.AddSeconds(3));
            AddNewChange(plugin, feature, expected, 1.8, time.AddSeconds(4));
            AddNewChange(plugin, feature, expected, 11.8, time.AddSeconds(5));

            var records = TestHelper.GetHistoryRecords(plugin, feature.Ref, 100);
            records.Reverse();
            CollectionAssert.AreEqual(expected, records);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();

            static void AddNewChange(Mock<PlugIn> plugin,
                                     HsFeature feature,
                                     IList<RecordData> expected,
                                     double value,
                                     DateTime lastChange)
            {
                var added = TestHelper.RaiseHSEventAndWait(plugin, Constants.HSEvent.VALUE_CHANGE, feature, value, value.ToString(), lastChange, expected.Count + 1);
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
                                     lastChange: time,
                                     apiType: (int)EApiType.Feature);

            Assert.IsTrue(plugin.Object.InitIO());
            List<RecordData> expected = new();

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, feature);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            feature.Changes[EProperty.Value] = 834D;
            feature.Changes[EProperty.DisplayedStatus] = "34324";
            // No time change is done here

            TestHelper.RaiseHSEvent(Constants.HSEvent.VALUE_CHANGE, plugin, feature);

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