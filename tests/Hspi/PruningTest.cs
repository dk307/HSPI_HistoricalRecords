using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using NUnit.Framework;
using Moq;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class PruningTest
    {
        [Test]
        public void PruningAccountsForPerDevicePruningDuration()
        {
            int refId = 3;
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(5);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = TestHelper.CreateMockSystemGlobalTimerAndClock(plugin);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.UtcNow).Returns(aTime.AddSeconds(10));

            mockHsController.SetupFeature(refId, 0D, "", aTime);

            //set device retention to custom value
            mockHsController.SetupIniValue("Settings", "DeviceSettings", refId.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "RefId", refId.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "IsTracked", true.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "RetentionPeriod", TimeSpan.FromSeconds(2).ToString("c"));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.That(plugin.Object.IsFeatureTracked(refId));

            int addedRecordCount = SettingsPages.MinRecordsToKeepDefault + 20;

            AddRecordsAndPrune(plugin, mockHsController, refId, aTime, addedRecordCount);

            Assert.That(TestHelper.WaitTillTotalRecords(plugin, refId, 112));

            Assert.That(plugin.Object.GetEarliestAndNewestRecordTotalSeconds(refId)[0], Is.EqualTo(10 - 8));
        }

        [Test]
        public void PruningPreservesMinRecords()
        {
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(1);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = TestHelper.CreateMockSystemGlobalTimerAndClock(plugin);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.UtcNow).Returns(aTime.AddSeconds(200));

            int refId = 3;
            mockHsController.SetupFeature(refId, 0D, "", aTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int addedRecordCount = SettingsPages.MinRecordsToKeepDefault + 20;

            AddRecordsAndPrune(plugin, mockHsController, refId, aTime, addedRecordCount);

            plugin.Object.PruneDatabase();

            Assert.That(TestHelper.WaitTillTotalRecords(plugin, refId, SettingsPages.MinRecordsToKeepDefault));

            // first 20 are gone
            Assert.That(plugin.Object.GetEarliestAndNewestRecordTotalSeconds(refId)[0], Is.EqualTo(200 - 20));
        }

        [Test]
        public void PruningRemovesOldestRecords()
        {
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(5);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = TestHelper.CreateMockSystemGlobalTimerAndClock(plugin);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.UtcNow).Returns(aTime.AddSeconds(10));

            int refId = 3;
            mockHsController.SetupFeature(refId, 0D, "", aTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int addedRecordCount = SettingsPages.MinRecordsToKeepDefault + 20;

            AddRecordsAndPrune(plugin, mockHsController, refId, aTime, addedRecordCount);

            plugin.Object.PruneDatabase();

            Assert.That(TestHelper.WaitTillTotalRecords(plugin, refId, 115));

            // first 5 are gone
            Assert.That(plugin.Object.GetEarliestAndNewestRecordTotalSeconds(refId)[0], Is.EqualTo(10 - 5));
        }

        private static void AddRecordsAndPrune(Mock<PlugIn> plugin, FakeHSController mockHsController, int refId, DateTime aTime, int addedRecordCount)
        {
            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         refId,
                                                         i, i.ToString(), aTime.AddSeconds(i),
                                                         i + 1));
            }

            Assert.That(addedRecordCount, Is.EqualTo(plugin.Object.GetTotalRecords(refId)));

            plugin.Object.PruneDatabase();
        }
    }
}