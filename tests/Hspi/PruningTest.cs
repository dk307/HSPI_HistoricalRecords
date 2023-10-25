using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class PruningTest
    {
        [TestMethod]
        public void PruningAccountsForPerDevicePruningDuration()
        {
            int deviceRefId = 3;
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(5);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = TestHelper.CreateMockSystemClock(plugin);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(10));

            var feature = TestHelper.SetupHsFeature(mockHsController, deviceRefId, 100);

            //set device retention to 10s
            mockHsController.Setup(x => x.GetINISetting("Settings", "DeviceSettings", null, PlugInData.SettingFileName)).Returns(deviceRefId.ToString());
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "DeviceRefId", null, PlugInData.SettingFileName)).Returns(deviceRefId.ToString());
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "IsTracked", null, PlugInData.SettingFileName)).Returns(true.ToString());
            mockHsController.Setup(x => x.GetINISetting(deviceRefId.ToString(), "RetentionPeriod", null, PlugInData.SettingFileName)).Returns(TimeSpan.FromSeconds(2).ToString("c"));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.IsTrue(plugin.Object.IsFeatureTracked(deviceRefId));

            int addedRecordCount = SettingsPages.MinRecordsToKeepDefault + 20;

            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         feature, i, i.ToString(), aTime.AddSeconds(i), i + 1));
            }

            Assert.AreEqual(plugin.Object.GetTotalRecords(feature.Ref), addedRecordCount);

            plugin.Object.PruneDatabase();

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 112));

            Assert.AreEqual(10 - 8, plugin.Object.GetEarliestAndOldestRecordTotalSeconds(feature.Ref)[0]);
        }

        [TestMethod]
        public void PruningPreservesMinRecords()
        {
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(1);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = TestHelper.CreateMockSystemClock(plugin);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(200));

            var feature = TestHelper.SetupHsFeature(mockHsController, 3, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int addedRecordCount = SettingsPages.MinRecordsToKeepDefault + 20;

            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         feature, i, i.ToString(), aTime.AddSeconds(i), i + 1));
            }
            Assert.AreEqual(plugin.Object.GetTotalRecords(feature.Ref), addedRecordCount);

            plugin.Object.PruneDatabase();

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, SettingsPages.MinRecordsToKeepDefault));

            // first 20 are gone
            Assert.AreEqual(200 - 20, plugin.Object.GetEarliestAndOldestRecordTotalSeconds(feature.Ref)[0]);
        }

        [TestMethod]
        public void PruningRemovesOldestRecords()
        {
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(5);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = TestHelper.CreateMockSystemClock(plugin);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(10));

            var feature = TestHelper.SetupHsFeature(mockHsController, 3, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int addedRecordCount = SettingsPages.MinRecordsToKeepDefault + 20;

            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         feature, i, i.ToString(), aTime.AddSeconds(i), i + 1));
            }
            Assert.AreEqual(plugin.Object.GetTotalRecords(feature.Ref), addedRecordCount);

            plugin.Object.PruneDatabase();

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 115));

            // first 5 are gone
            Assert.AreEqual(10 - 5, plugin.Object.GetEarliestAndOldestRecordTotalSeconds(feature.Ref)[0]);
        }
    }
}