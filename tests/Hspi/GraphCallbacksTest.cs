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
        [TestMethod]
        public void GetRecordsWithGroupingAndLOCF()
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

            for (var i = 0; i < MaxGraphPoints * 2; i++)
            {
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                        deviceRefId, i, "33", time.AddSeconds(i * 5), i + 1);
            }

            string format = $"{{ refId:{deviceRefId}, min:{time.ToUnixTimeMilliseconds()}, max:{mockHsController.GetFeatureLastChange(deviceRefId).AddSeconds(4).ToUnixTimeMilliseconds()}, fill:'0', points:{MaxGraphPoints}}}";
            string data = plugin.Object.PostBackProc("graphrecords", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var result = (JArray)jsonData["result"]["data"];
            Assert.AreEqual(10, (int)jsonData["result"]["groupedbyseconds"]);

            Assert.AreEqual(MaxGraphPoints, result.Count);

            for (var i = 0; i < MaxGraphPoints; i++)
            {
                long ts = ((DateTimeOffset)time.AddSeconds(i * 10)).ToUnixTimeSeconds() * 1000;
                Assert.AreEqual(ts, (long)result[i]["x"]);

                var value = (i * 2D + (i * 2) + 1D) / 2D;
                Assert.AreEqual(value, (double)result[i]["y"]);
            }
        }

        [DataTestMethod]
        [DataRow(FillStrategy.LOCF)]
        [DataRow(FillStrategy.Linear)]
        public void GetRecordsWithUpscaling(FillStrategy fillStrategy)
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

            var added = new List<RecordData>();
            for (var i = 0; i < 100; i++)
            {
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                        deviceRefId, i, "33", time.AddSeconds(i * 5), i + 1));
            }

            long max = mockHsController.GetFeatureLastChange(deviceRefId).ToUnixTimeMilliseconds();
            long min = time.ToUnixTimeMilliseconds();
            string format = $"{{ refId:{deviceRefId}, min:{min}, max:{max}, fill:{(int)fillStrategy}, points:{max - min}}}";
            string data = plugin.Object.PostBackProc("graphrecords", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var result = (JArray)jsonData["result"]["data"];
            Assert.AreEqual(1, (int)jsonData["result"]["groupedbyseconds"]);
            int expectedCount = (int)(1 + (max - min) / 1000);
            Assert.AreEqual(expectedCount, result.Count);

            if (fillStrategy == FillStrategy.LOCF)
            {
                for (var i = 0; i < expectedCount - 1; i++)
                {
                    long ts = ((DateTimeOffset)time.AddSeconds(i)).ToUnixTimeSeconds() * 1000;
                    Assert.AreEqual(ts, (long)result[i]["x"]);
                    int expectedRecord = i / 5;
                    Assert.AreEqual(added[expectedRecord].DeviceValue, (double)result[i]["y"]);
                }

                Assert.AreEqual(max, (long)result[result.Count - 1]["x"]);
                Assert.AreEqual(added[^1].DeviceValue, (double)result[result.Count - 1]["y"]);
            }
            else if (fillStrategy == FillStrategy.Linear)
            {
                for (var i = 0; i < expectedCount - 1; i++)
                {
                    long ts = ((DateTimeOffset)time.AddSeconds(i)).ToUnixTimeSeconds() * 1000;
                    Assert.AreEqual(ts, (long)result[i]["x"]);
                    var expectedValue = (double)((long)Math.Round((0.1D + (i * 0.2D)) * 10000D)) / 10000D; // *10000 and /10000 to fix float round issues
                    Assert.AreEqual(expectedValue, (double)result[i]["y"]);
                }

                Assert.AreEqual(max, (long)result[result.Count - 1]["x"]);
                Assert.AreEqual(added[^1].DeviceValue, (double)result[result.Count - 1]["y"]);
            }
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("", "Data is not correct")]
        [DataRow("refId={0}&min=1001&max=99", "Unexpected character encountered")]
        [DataRow("{{ refId:{0}, min:1001, max:99}}", "Max is less than min")]
        [DataRow("{{ refId:{0}, min:'abc', max:99 }}", "Min is not correct")]
        [DataRow("{{ refId:{0}, min:33, max:'abc' }}", "Max is not correct")]
        [DataRow("{{refId:{0}}}", "Min is not correct")]
        [DataRow("{{refId1:{0}}}", "Ref id is not correct")]
        [DataRow("{{ refId:{0}, min:11, max:99}}", "Fill is not correct")]
        [DataRow("{{ refId:{0}, min:11, max:99, fill:'rt'}}", "Fill is not correct")]
        [DataRow("{{ refId:{0}, min:11, max:99, fill:'5'}}", "Fill is not correct")]
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

        private const int MaxGraphPoints = 256;
    }
}