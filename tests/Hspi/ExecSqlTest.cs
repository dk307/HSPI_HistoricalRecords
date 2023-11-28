using System;
using System.Collections.Generic;
using HomeSeer.PluginSdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static SQLitePCL.Ugly.ugly;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class ExecSqlTest
    {
        [TestMethod]
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

            Assert.IsNotNull(data);
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual((long)refId, data[0]["ref"]);
            Assert.AreEqual(1.1D, data[0]["value"]);
            Assert.AreEqual("1.1", data[0]["str"]);
            Assert.AreEqual((long)refId, data[1]["ref"]);
            Assert.AreEqual(10D, data[1]["value"]);
            Assert.AreEqual("10", data[1]["str"]);
        }

        [TestMethod]
        public void ExecSqlAsFunctionWithError()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.ThrowsException<sqlite3_exception>(() => plugin.Object.ExecSql(@"SELECT ref1, value, str FROM history"));
        }

        [TestMethod]
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
            Assert.IsNotNull(json);

            var columns = json["result"]["columns"] as JArray;
            Assert.IsNotNull(columns);
            Assert.AreEqual(1, columns.Count);
            Assert.AreEqual("TotalCount", columns[0].ToString());

            var data = json["result"]["data"] as JArray;
            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual((long)45, data[0][0]);
        }

        [TestMethod]
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
            Assert.IsNotNull(json);

            var columns = json["result"]["columns"] as JArray;
            Assert.IsNotNull(columns);
            Assert.AreEqual(3, columns.Count);
            Assert.AreEqual("ref", columns[0].ToString());
            Assert.AreEqual("value", columns[1].ToString());
            Assert.AreEqual("str", columns[2].ToString());

            var data = json["result"]["data"] as JArray;
            Assert.IsNotNull(data);
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual((long)refId, data[0][0]);
            Assert.AreEqual(1.1D, data[0][1]);
            Assert.AreEqual("1.1", data[0][2]);
            Assert.AreEqual((long)refId, data[1][0]);
            Assert.AreEqual(10D, data[1][1]);
            Assert.AreEqual("10", data[1][2]);
        }

        [TestMethod]
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
            Assert.IsNotNull(json);

            var columns = json["result"]["columns"] as JArray;
            Assert.IsNotNull(columns);
            Assert.AreEqual(0, columns.Count);

            var data = json["result"]["data"] as JArray;
            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.Count);
        }

        [TestMethod]
        public void ExecSqlAsPostWithError()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var jsonString = plugin.Object.PostBackProc("execsql", @"{sql: 'SELECT COUNT(*) AS TotalCount FROM history1'}", string.Empty, 0);

            var json = (JObject)JsonConvert.DeserializeObject(jsonString);
            Assert.IsNotNull(json);

            var error = (string)json["error"];
            Assert.AreEqual("no such table: history1", error);
        }
    }
}