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
            DateTime nowTime = new(2222, 2, 2, 2, 2, 2);
            mockClock.Setup(x => x.Now).Returns(nowTime.AddSeconds(10));

            var feature = TestHelper.SetupHsFeature(mockHsController, 3, 100);

            Assert.IsTrue(plugin.Object.InitIO());

            int addedRecordCount = SettingsPages.MinRecordsToKeepDefault + 10;

            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, Constants.HSEvent.VALUE_CHANGE,
                                                         feature, i, i.ToString(), nowTime.AddSeconds(i), i + 1));
            }
            Assert.AreEqual(plugin.Object.GetTotalRecords(feature.Ref), addedRecordCount);

            plugin.Object.PruneDatabase();

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 105));

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }
    }
}