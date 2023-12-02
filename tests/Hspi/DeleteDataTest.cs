using System;
using HomeSeer.PluginSdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class DeleteDataTest
    {
        [Test]
        public void DeleteOrphanDataOnStart()
        {
            var plugin1 = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin1);

            int refId = 1000;

            mockHsController.SetupFeature(refId, 1.1);
            using (PlugInLifeCycle plugInLifeCycle1 = new(plugin1))
            {
                Assert.That(TestHelper.WaitTillTotalRecords(plugin1, refId, 1));
            }

            mockHsController.RemoveFeatureOrDevice(refId);
            var plugin2 = TestHelper.CreatePlugInMock();
            TestHelper.UpdatePluginHsGet(plugin2, mockHsController);

            using PlugInLifeCycle plugInLifeCycle2 = new(plugin2);
            Assert.That(TestHelper.WaitTillTotalRecords(plugin2, refId, 0));
        }

        [Test]
        public void DeleteRecordsOnTrackedFeatureDelete()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            int refId = 10002;

            hsControllerMock.SetupFeature(refId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            Assert.That(TestHelper.WaitTillTotalRecords(plugIn, refId, 1));

            plugIn.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE,
                                  new object[] { null, null, null, refId, 2 });

            Assert.That(TestHelper.WaitTillTotalRecords(plugIn, refId, 0));
        }

        [Test]
        public void HandleDeleteRecords()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            int refId = 10002;

            hsControllerMock.SetupFeature(refId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            Assert.That(TestHelper.WaitTillTotalRecords(plugIn, refId, 1));

            plugIn.Object.PostBackProc("deletedevicerecords", $"{{ref:{refId}}}", string.Empty, 0);

            Assert.That(TestHelper.WaitTillTotalRecords(plugIn, refId, 0));
        }

        [Test]
        public void HandleStatisticsForRecords()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugin);

            DateTime time = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 10002;

            hsControllerMock.SetupFeature(refId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, refId, 1);

            TestHelper.RaiseHSEventAndWait(plugin, hsControllerMock, Constants.HSEvent.VALUE_CHANGE,
                                    refId, 10, "10", time, 1);
            TestHelper.RaiseHSEventAndWait(plugin, hsControllerMock, Constants.HSEvent.VALUE_CHANGE,
                                    refId, 100, "100", time.AddSeconds(60), 2);

            string format = $"{{ refId:{refId}, min:{time.ToUnixTimeMilliseconds()}, max:{time.AddSeconds(119).ToUnixTimeMilliseconds()}}}";
            string data = plugin.Object.PostBackProc("statisticsforrecords", format, string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var result = (JArray)jsonData["result"]["data"];
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That((double)result[0], Is.EqualTo((10D * 60 + 100D * 60) / 120D));
            Assert.That((double)result[1], Is.EqualTo((55D * 60 + 100D * 60) / 120D));
            Assert.That((double)result[2], Is.EqualTo(10D));
            Assert.That((double)result[3], Is.EqualTo(100D));
        }
    }
}