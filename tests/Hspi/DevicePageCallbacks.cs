using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq.Protected;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class DevicePageCallbacks
    {
        [TestMethod]
        public void GetOldestRecordTimeDate()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            HsFeature feature;
            DateTime nowTime = new DateTime(2222, 2, 2, 2, 2, 2);

            feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime,
                                                apiType: (int)EApiType.Feature);

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(Constants.HSEvent.STRING_CHANGE, plugin, feature);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.STRING_CHANGE, feature, 345, "43", nowTime.AddSeconds(-1000));

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 2));

            plugin.Protected().Setup<DateTimeOffset>("TimeNow").Returns((DateTimeOffset)nowTime);

            long oldestRecord = plugin.Object.GetOldestRecordTimeDate(feature.Ref.ToString());
            Assert.AreEqual(1000, oldestRecord);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }
    }
}