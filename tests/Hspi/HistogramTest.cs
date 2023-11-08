using System;
using HomeSeer.PluginSdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class HistogramTest
    {
        [TestMethod]
        public void WithoutHittingMaxCount()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);

            DateTime time = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int deviceRefId = 35673;
            mockHsController.SetupFeature(deviceRefId,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1);

            int maxCount = 10;
            string format = $"{{ refId:{deviceRefId}, min:{time.ToUnixTimeMilliseconds()}, max:{time.AddSeconds(10).ToUnixTimeMilliseconds()}, count:'{maxCount}'}}";
            string data = plugin.Object.PostBackProc("histogramforrecords", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var labels = (JArray)jsonData["result"]["labels"];
            Assert.AreEqual(1, labels.Count);
            Assert.AreEqual("1.1", labels[0]);

            var times = (JArray)jsonData["result"]["time"];
            Assert.AreEqual(1, times.Count);
            Assert.AreEqual(11000, times[0]);
        }

        [TestMethod]
        public void NoData()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);

            DateTime time = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int deviceRefId = 35673;
            mockHsController.SetupFeature(deviceRefId,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            TestHelper.WaitForRecordCountAndDeleteAll(plugin, deviceRefId, 1);

            int maxCount = 10;
            string format = $"{{ refId:{deviceRefId}, min:{time.ToUnixTimeMilliseconds()}, max:{time.AddSeconds(10).ToUnixTimeMilliseconds()}, count:'{maxCount}'}}";
            string data = plugin.Object.PostBackProc("histogramforrecords", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var labels = (JArray)jsonData["result"]["labels"];
            Assert.AreEqual(0, labels.Count);

            var times = (JArray)jsonData["result"]["time"];
            Assert.AreEqual(0, times.Count);
        }

        [TestMethod]
        public void WithHittingMaxCount()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);

            DateTime time = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int deviceRefId = 35673;
            mockHsController.SetupFeature(deviceRefId,
                                     1.1,
                                     displayString: "1.1",
                                     lastChange: time);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            TestHelper.WaitForRecordCountAndDeleteAll(plugin, deviceRefId, 1);

            var prevTime = time;
            for (int i = 0; i < 20; i++)
            {
                double val = 10 + i;
                prevTime = prevTime.AddSeconds(i);
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                               deviceRefId, val, val.ToString(), prevTime, i + 1);
            }

            int maxCount = 10;
            string format = $"{{ refId:{deviceRefId}, min:{time.ToUnixTimeMilliseconds()}, max:{prevTime.AddSeconds(19).ToUnixTimeMilliseconds()}, count:'{maxCount}'}}";
            string data = plugin.Object.PostBackProc("histogramforrecords", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var labels = (JArray)jsonData["result"]["labels"];
            Assert.AreEqual(10, labels.Count);
            Assert.IsTrue(labels.Select(x => x.Value<string>()).SequenceEqual(
                            new string[] { "29", "28", "27", "26", "25", "24", "23", "22", "21", null }));

            var times = (JArray)jsonData["result"]["time"];
            Assert.AreEqual(10, times.Count);
            Assert.IsTrue(times.Select(x => x.Value<long>()).SequenceEqual(
                  new long[] { 20000, 19000, 18000, 17000, 16000, 15000, 14000, 13000, 12000, 11000 + 10000 + 9000 + 8000 + 7000 + 6000 + 5000 + 4000 + 3000 + 2000 + 1000 }));
        }
    }
}