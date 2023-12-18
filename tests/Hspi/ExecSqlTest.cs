using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static SQLitePCL.Ugly.ugly;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class ExecSqlTest
    {
        [Test]
        public void ExecSqlAsFunctionSingleFeatureAllValues()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 100;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, refId, 1);
            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                           refId, 10, 10.ToString(), nowTime.AddSeconds(1), 2);

            var data = plugin.Object.ExecSql(@"SELECT ref, value, str FROM history");

            Assert.That(data, Is.Not.Null);
            Assert.That(data.Count, Is.EqualTo(2));
            Assert.That(data[0]["ref"], Is.EqualTo((long)refId));
            Assert.That(data[0]["value"], Is.EqualTo(1.1D));
            Assert.That(data[0]["str"], Is.EqualTo("1.1"));
            Assert.That(data[1]["ref"], Is.EqualTo((long)refId));
            Assert.That(data[1]["value"], Is.EqualTo(10D));
            Assert.That(data[1]["str"], Is.EqualTo("10"));
        }

        [Test]
        public void ExecSqlAsFunctionWithError()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.Catch<sqlite3_exception>(() => plugin.Object.ExecSql(@"SELECT ref1, value, str FROM history"));
        }

        [Test]
        public void ExecSqlAsPostCount()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            List<int> hsFeatures = new();
            for (int i = 0; i < 10; i++)
            {
                mockHsController.SetupFeature(1307 + i, 1.1, displayString: "1.1", lastChange: nowTime);
                hsFeatures.Add(1307 + i);
            }

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            for (int i = 0; i < hsFeatures.Count; i++)
            {
                TestHelper.WaitForRecordCountAndDeleteAll(plugin, hsFeatures[i], 1);
                for (int j = 0; j < i; j++)
                {
                    TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                   hsFeatures[i], i, i.ToString(), nowTime.AddMinutes(i * j), j + 1);
                }
            }

            var jsonString = plugin.Object.PostBackProc("execsql", @"{sql: 'SELECT COUNT(*) AS TotalCount FROM history'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.That(json, Is.Not.Null);

            var columns = json["result"]["columns"] as JArray;
            Assert.That(columns, Is.Not.Null);
            Assert.That(columns.Count, Is.EqualTo(1));
            Assert.That(columns[0].ToString(), Is.EqualTo("TotalCount"));

            var data = json["result"]["data"] as JArray;
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Count, Is.EqualTo(1));
            Assert.That((long)data[0][0], Is.EqualTo(45L));
        }

        [Test]
        public void ExecSqlAsPostSingleFeatureAllValues()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 100;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, refId, 1);
            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                           refId, 10, 10.ToString(), nowTime.AddSeconds(1), 2);

            var jsonString = plugin.Object.PostBackProc("execsql", @"{sql: 'SELECT ref, value, str FROM history'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.That(json, Is.Not.Null);

            var columns = json["result"]["columns"] as JArray;
            Assert.That(columns, Is.Not.Null);
            Assert.That(columns.Count, Is.EqualTo(3));
            Assert.That(columns[0].ToString(), Is.EqualTo("ref"));
            Assert.That(columns[1].ToString(), Is.EqualTo("value"));
            Assert.That(columns[2].ToString(), Is.EqualTo("str"));

            var data = json["result"]["data"] as JArray;
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Count, Is.EqualTo(2));
            Assert.That((long)data[0][0], Is.EqualTo((long)refId));
            Assert.That((double)data[0][1], Is.EqualTo(1.1D));
            Assert.That((string)data[0][2], Is.EqualTo("1.1"));
            Assert.That((long)data[1][0], Is.EqualTo((long)refId));
            Assert.That((double)data[1][1], Is.EqualTo(10D));
            Assert.That((string)data[1][2], Is.EqualTo("10"));
        }

        [Test]
        public void ExecSqlAsPostVacuum()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 100;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, refId, 1);
            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                           refId, 10, 10.ToString(), nowTime.AddSeconds(1), 2);

            var jsonString = plugin.Object.PostBackProc("execsql", "{sql: 'VACUUM'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.That(json, Is.Not.Null);

            var columns = json["result"]["columns"] as JArray;
            Assert.That(columns, Is.Not.Null);
            Assert.That(columns.Count, Is.EqualTo(0));

            var data = json["result"]["data"] as JArray;
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExecSqlAsPostWithError()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var jsonString = plugin.Object.PostBackProc("execsql", @"{sql: 'SELECT COUNT(*) AS TotalCount FROM history1'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.That(json, Is.Not.Null);

            var error = (string)json["error"];
            Assert.That(error, Is.EqualTo("no such table: history1"));
        }
    }
}