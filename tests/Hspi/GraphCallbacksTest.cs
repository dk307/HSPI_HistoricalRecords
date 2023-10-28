using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using Hspi;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class GraphCallbacksTest
    {
        [DataTestMethod]
        [DataRow(FillStrategy.LOCF)]
        [DataRow(FillStrategy.Linear)]
        public void GetRecordsWithoutGrouping(FillStrategy fillStrategy)
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

            for (var i = 0; i < 100; i++)
            {
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                        deviceRefId, i, "33", time.AddSeconds(i * 5), i + 1);
            }

            string format = $"{{ refId:{deviceRefId}, min:{((DateTimeOffset)time).ToUnixTimeMilliseconds()}, max:{((DateTimeOffset)mockHsController.GetFeature(deviceRefId).LastChange).ToUnixTimeMilliseconds()}, fill:'{fillStrategy}'}}";
            string data = plugin.Object.PostBackProc("graphrecords", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var result = (JArray)jsonData["result"]["data"];
            Assert.AreEqual(0, (int)jsonData["result"]["groupedbyseconds"]);
            Assert.AreEqual(100, result.Count);

            for (var i = 0; i < 100; i++)
            {
                long ts = ((DateTimeOffset)time.AddSeconds(i * 5)).ToUnixTimeSeconds() * 1000;
                Assert.AreEqual(ts, (long)result[i]["x"]);
                Assert.AreEqual((double)i, (double)result[i]["y"]);
            }
        }

        [TestMethod]
        public void GetRecordsWithGroupingAndLOCF()
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

            for (var i = 0; i < PlugIn.MaxGraphPoints * 2; i++)
            {
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                        deviceRefId, i, "33", time.AddSeconds(i * 5), i + 1);
            }

            string format = $"{{ refId:{deviceRefId}, min:{((DateTimeOffset)time).ToUnixTimeMilliseconds()}, max:{((DateTimeOffset)mockHsController.GetFeature(deviceRefId).LastChange.AddSeconds(4)).ToUnixTimeMilliseconds()}, fill:'locf'}}";
            string data = plugin.Object.PostBackProc("graphrecords", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var result = (JArray)jsonData["result"]["data"];
            Assert.AreEqual(10, (int)jsonData["result"]["groupedbyseconds"]);

            Assert.AreEqual(PlugIn.MaxGraphPoints, result.Count);

            for (var i = 0; i < PlugIn.MaxGraphPoints; i++)
            {
                long ts = ((DateTimeOffset)time.AddSeconds(i * 10)).ToUnixTimeSeconds() * 1000;
                Assert.AreEqual(ts, (long)result[i]["x"]);

                var value = (i * 2D + (i * 2) + 1D) / 2D;
                Assert.AreEqual(value, (double)result[i]["y"]);
            }
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("", "refId is not correct")]
        [DataRow("refId={0}&min=1001&max=99", "Unexpected character encountered")]
        [DataRow("{{ refId:{0}, min:1001, max:99}}", "max < min")]
        [DataRow("{{ refId:{0}, min:'abc', max:99 }}", "min is not correct")]
        [DataRow("{{ refId:{0}, min:33, max:'abc' }}", "max is not correct")]
        [DataRow("{{refId:{0}}}", "min is not correct")]
        [DataRow("{{refId1:{0}}}", "refId is not correct")]
        [DataRow("{{ refId:{0}, min:11, max:99}}", "fill is not correct")]
        [DataRow("{{ refId:{0}, min:11, max:99, fill:'rt'}}", "fill is not correct")]
        public void GraphCallbackArgumentChecks(string format, string exception)
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings2(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devRefId = 1938;
            string paramsForRecord = String.Format(format, devRefId);

            string data = plugin.Object.PostBackProc("graphrecords", paramsForRecord, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, exception);
        }
    }
}