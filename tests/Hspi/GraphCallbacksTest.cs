using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using Hspi;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class GraphCallbacksTest
    {
        [Test]
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
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var result = (JArray)jsonData["result"]["data"];
            Assert.That((int)jsonData["result"]["groupedbyseconds"], Is.EqualTo(10));

            Assert.That(result.Count, Is.EqualTo(MaxGraphPoints));

            for (var i = 0; i < MaxGraphPoints; i++)
            {
                long ts = ((DateTimeOffset)time.AddSeconds(i * 10)).ToUnixTimeSeconds() * 1000;
                Assert.That((long)result[i]["x"], Is.EqualTo(ts));

                var value = (i * 2D + (i * 2) + 1D) / 2D;
                Assert.That((double)result[i]["y"], Is.EqualTo(value));
            }
        }

        [TestCase(FillStrategy.LOCF)]
        [TestCase(FillStrategy.Linear)]
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
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var result = (JArray)jsonData["result"]["data"];
            Assert.That((int)jsonData["result"]["groupedbyseconds"], Is.EqualTo(1));
            int expectedCount = (int)(1 + (max - min) / 1000);
            Assert.That(result.Count, Is.EqualTo(expectedCount));

            if (fillStrategy == FillStrategy.LOCF)
            {
                for (var i = 0; i < expectedCount - 1; i++)
                {
                    long ts = ((DateTimeOffset)time.AddSeconds(i)).ToUnixTimeSeconds() * 1000;
                    Assert.That((long)result[i]["x"], Is.EqualTo(ts));
                    int expectedRecord = i / 5;
                    Assert.That((double)result[i]["y"], Is.EqualTo(added[expectedRecord].DeviceValue));
                }

                Assert.That((long)result[result.Count - 1]["x"], Is.EqualTo(max));
                Assert.That((double)result[result.Count - 1]["y"], Is.EqualTo(added[^1].DeviceValue));
            }
            else if (fillStrategy == FillStrategy.Linear)
            {
                for (var i = 0; i < expectedCount - 1; i++)
                {
                    long ts = ((DateTimeOffset)time.AddSeconds(i)).ToUnixTimeSeconds() * 1000;
                    Assert.That((long)result[i]["x"], Is.EqualTo(ts));
                    var expectedValue = (double)((long)Math.Round((0.1D + (i * 0.2D)) * 10000D)) / 10000D; // *10000 and /10000 to fix float round issues
                    Assert.That((double)result[i]["y"], Is.EqualTo(expectedValue));
                }

                Assert.That((long)result[result.Count - 1]["x"], Is.EqualTo(max));
                Assert.That((double)result[result.Count - 1]["y"], Is.EqualTo(added[^1].DeviceValue));
            }
        }

        [TestCase("", "data is not correct")]
        [TestCase("refId={0}&min=1001&max=99", "Unexpected character encountered")]
        [TestCase("{{ refId:{0}, min:1001, max:99}}", "max is less than min")]
        [TestCase("{{ refId:{0}, min:'abc', max:99 }}", "min is not correct")]
        [TestCase("{{ refId:{0}, min:33, max:'abc' }}", "max is not correct")]
        [TestCase("{{refId:{0}}}", "min is not correct")]
        [TestCase("{{refId1:{0}}}", "refId is not correct")]
        [TestCase("{{ refId:{0}, min:11, max:99}}", "fill is not correct")]
        [TestCase("{{ refId:{0}, min:11, max:99, fill:'rt'}}", "fill is not correct")]
        [TestCase("{{ refId:{0}, min:11, max:99, fill:'5'}}", "fill is not correct")]
        public void GraphCallbackArgumentChecks(string format, string exception)
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings2(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devRefId = 1938;
            string paramsForRecord = String.Format(format, devRefId);

            string data = plugin.Object.PostBackProc("graphrecords", paramsForRecord, string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.That(!string.IsNullOrWhiteSpace(errorMessage));
            Assert.That(errorMessage, Does.Contain(exception));
        }

        private const int MaxGraphPoints = 256;
    }
}