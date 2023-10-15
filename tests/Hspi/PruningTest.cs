using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class PruningTest
    {
        [TestMethod]
        public void PruningRemovesOldestRecords()
        {
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(5);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = new Mock<ISystemClock>(MockBehavior.Strict);
            plugin.Protected().Setup<ISystemClock>("CreateClock").Returns(mockClock.Object);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(10));

            var feature = TestHelper.SetupHsFeature(mockHsController, 3, 100);

            Assert.IsTrue(plugin.Object.InitIO());

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
            Assert.AreEqual(10 - 5, plugin.Object.GetEarliestAndOldestRecordTotalSeconds(feature.Ref.ToString())[0]);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void PruningPrservesMinRecords()
        {
            TimeSpan pruningTimePeriod = TimeSpan.FromSeconds(1);

            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin,
                new Dictionary<string, string>() { { "GlobalRetentionPeriod", pruningTimePeriod.ToString() } });

            var mockClock = new Mock<ISystemClock>(MockBehavior.Strict);
            plugin.Protected().Setup<ISystemClock>("CreateClock").Returns(mockClock.Object);
            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(200));

            var feature = TestHelper.SetupHsFeature(mockHsController, 3, 100);

            Assert.IsTrue(plugin.Object.InitIO());

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
            Assert.AreEqual(200 - 20, plugin.Object.GetEarliestAndOldestRecordTotalSeconds(feature.Ref.ToString())[0]);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }
    }
}