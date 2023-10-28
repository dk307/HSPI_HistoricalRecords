using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class ScrbianFunctionsTest
    {
        [TestMethod]
        public void GetAllDevicesProperties()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = DateTime.Now;

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
            Assert.IsNotNull(stats);
            Assert.AreEqual(15, stats.Count);

            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(1307 + i, stats[i]["ref"]);
                Assert.AreEqual((long)i, stats[i]["records"]);
                Assert.AreEqual(true, stats[i]["monitorableType"]);
                Assert.AreEqual(true, stats[i]["tracked"]);
            }
        }

        [TestMethod]
        public void GetDatabaseStats()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = DateTime.Now;

            int ref1 = 42;
            int ref2 = 43;

            mockHsController.SetupFeature(ref1, 1.1, displayString: "1.1", lastChange: nowTime);
            mockHsController.SetupFeature(ref2, 1.1, displayString: "4.5", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            for (int i = 0; i < 10; i++)
            {
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         ref1, i, i.ToString(), nowTime.AddMinutes(i), i + 1);
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         ref2, i, i.ToString(), nowTime.AddMinutes(i), i + 1);
            }

            var stats = plugin.Object.GetDatabaseStats();
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.ContainsKey("Path"));
            Assert.IsTrue(stats.ContainsKey("Sqlite version"));
            Assert.IsTrue(stats.ContainsKey("Sqlite memory used"));
            Assert.IsTrue(stats.ContainsKey("Size"));
            Assert.AreEqual("20", stats["Total records"]);
            Assert.AreEqual("20", stats["Total records from last 24 hr"]);
        }
    }
}