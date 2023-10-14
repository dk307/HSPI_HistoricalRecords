﻿using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class DevicePageCallbacks
    {
        public static IEnumerable<object[]> GetDatatableCallbacksData()
        {
            // 1) record limit  = 100, start = 0, length = 10, no ordering specified
            yield return new object[] {
                new  Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&recordLimit=100&start=0&length=10"),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new TimeComparer());
                    records.Reverse();
                    return records.Take(10).ToList();
                } )
            };

            // 2) record limit = 100, start = 10, length = 10, no ordering specified
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&recordLimit=100&start=10&length=10"),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new TimeComparer());
                    records.Reverse();
                    return records.Skip(10).Take(10).ToList();
                } )
            };

            // 3) record limit = 100, start = 10, length = 10, time sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&recordLimit=100&start=10&length=10&order[0][column]=0&order[0][dir]=asc"),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new TimeComparer());
                    return records.Skip(10).Take(10).ToList();
                } )
            };

            // 4) record limit = 1000, start = 0, length = 1000, string sort, desc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&recordLimit=1000&start=0&length=1000&order[0][column]=2&order[0][dir]=desc"),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new StringValueComparer());
                    records.Reverse();
                    return records.Take(1000).ToList();
                } )
            };

            // 5) min , max, start = 0, length = 10, time sort ,desc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[10].UnixTimeMilliSeconds;
                    var max = records[30].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=0&length=10&order[0][column]=0&order[0][dir]=desc";
                }),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new TimeComparer());
                    return records.Skip(10).Take(30 - 10 + 1).Reverse().Take(10).ToList();
                } )
            };

            // 6) min , max, start = 10, length = 100, value sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[80].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=100&order[0][column]=1&order[0][dir]=desc";
                }),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new TimeComparer());
                    var newList = records.Skip(20).Take(80 - 20 + 1).ToList();
                    newList.Sort(new ValueComparer());
                    newList.Reverse();
                    return newList.Skip(10).Take(100).ToList();
                } )
             };
        }

        public static IEnumerable<object[]> GetDatatableCallbackTotalData()
        {
            // 1) record limit  = 100, start = 0, length = 10, no ordering specified
            yield return new object[] {
                new  Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&recordLimit=100&start=0&length=10"),
                10, 10
            };

            // 2) record limit = 100, start = 10, length = 10, no ordering specified
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&recordLimit=100&start=10&length=50"),
                10, 10
            };

            // 3) record limit = 10, start = 10, length = 10, time sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&recordLimit=100&start=10&length=10&order[0][column]=0&order[0][dir]=asc"),
               100, 100
            };

            // 5) min , max, start = 0, length = 10, time sort ,desc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[10].UnixTimeMilliSeconds;
                    var max = records[30].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=0&length=10&order[0][column]=0&order[0][dir]=desc";
                }),
                100, 21
            };

            // 6) min , max, start = 10, length = 100, value sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[80].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=100&order[0][column]=1&order[0][dir]=desc";
                }),
                100, 61
             };
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("refId={0}&min=1001&max=99&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "max < min")]
        [DataRow("refId={0}&min=abc&max=99&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "Parameter name: min")]
        [DataRow("refId={0}&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "Neither min/max or recordCount specified")]
        public void DatatableCallbackMinMoreThanMax(string format, string exception)
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            Assert.IsTrue(plugin.Object.InitIO());

            int devRefId = 1938;
            string paramsForRecord = String.Format(format, devRefId);

            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, exception);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        [DynamicData(nameof(GetDatatableCallbackTotalData), DynamicDataSourceType.Method)]
        public void DatatableCallbackTotal(Func<HsFeature, List<RecordData>, string> createString, int addedRecordCount, int total)
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime nowTime = DateTime.Now;

            var feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime);

            Assert.IsTrue(plugin.Object.InitIO());

            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                double val = 1000 - i;
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         feature, val, val.ToString(), nowTime.AddMinutes(i), i + 1));
            }

            string paramsForRecord = createString(feature, added.Clone());

            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var recordsTotal = jsonData["recordsTotal"].Value<long>();
            Assert.AreEqual(total, recordsTotal);

            var recordsFiltered = jsonData["recordsFiltered"].Value<long>();
            Assert.AreEqual(total, recordsFiltered);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        [DynamicData(nameof(GetDatatableCallbacksData), DynamicDataSourceType.Method)]
        public void DatatableCallbackTotalData(Func<HsFeature, List<RecordData>, string> createString, Func<List<RecordData>, List<RecordData>> filter)
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime nowTime = DateTime.Now;

            var feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime);

            Assert.IsTrue(plugin.Object.InitIO());

            var added = new List<RecordData>();
            for (int i = 0; i < 100; i++)
            {
                double val = 1000 - i;
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         feature, val, val.ToString(), nowTime.AddMinutes(i), i + 1));
            }

            string paramsForRecord = createString(feature, added.Clone());
            var records = TestHelper.GetHistoryRecords(plugin, feature.Ref, paramsForRecord);
            Assert.IsNotNull(records);

            var filterRecords = filter(added.Clone());

            CollectionAssert.AreEqual(records, filterRecords);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void GetDatabaseStats()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime nowTime = DateTime.Now;

            var feature1 = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime);
            var feature2 = TestHelper.SetupHsFeature(mockHsController, 374, 1.1, displayString: "4.5", lastChange: nowTime);

            Assert.IsTrue(plugin.Object.InitIO());

            for (int i = 0; i < 10; i++)
            {
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         feature1, i, i.ToString(), nowTime.AddMinutes(i), i + 1);
                TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         feature2, i, i.ToString(), nowTime.AddMinutes(i), i + 1);
            }

            var stats = plugin.Object.GetDatabaseStats();
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.ContainsKey("Path"));
            Assert.IsTrue(stats.ContainsKey("Sqlite version"));
            Assert.IsTrue(stats.ContainsKey("Sqlite memory used"));
            Assert.IsTrue(stats.ContainsKey("Size"));
            Assert.AreEqual(stats["Total records"], "20");
            Assert.AreEqual(stats["Total records from last 24 hr"], "20");

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void GetTop10RecordsStats()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime nowTime = DateTime.Now;

            List<HsFeature> hsFeatures = new List<HsFeature>();

            Assert.IsTrue(plugin.Object.InitIO());
            for (int i = 0; i < 15; i++)
            {
                hsFeatures.Add(TestHelper.SetupHsFeature(mockHsController, 1307 + i, 1.1, displayString: "1.1", lastChange: nowTime));
                for (int j = 0; j < i; j++)
                {
                    TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                   hsFeatures[i], i, i.ToString(), nowTime.AddMinutes(i * j), j + 1);
                }
            }

            var stats = plugin.Object.GetTop10RecordsStats();
            Assert.IsNotNull(stats);
            Assert.AreEqual(10, stats.Count);
            Assert.AreEqual(stats[0].Key, 1307 + 14);
            Assert.AreEqual(stats[0].Value, 14);
            Assert.AreEqual(stats[9].Key, 1307 + 5);
            Assert.AreEqual(stats[9].Value, 5);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void GetOldestRecordTimeDate()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            var mockClock = new Mock<ISystemClock>(MockBehavior.Strict);
            plugin.Protected().Setup<ISystemClock>("CreateClock").Returns(mockClock.Object);
            DateTime nowTime = new(2222, 2, 2, 2, 2, 2);
            mockClock.Setup(x => x.Now).Returns(nowTime);

            var feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime);

            Assert.IsTrue(plugin.Object.InitIO());

            TestHelper.RaiseHSEvent(plugin, mockHsController, feature, Constants.HSEvent.STRING_CHANGE);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 1));

            TestHelper.RaiseHSEvent(plugin, mockHsController, Constants.HSEvent.STRING_CHANGE, feature, 345, "43", nowTime.AddSeconds(-1000));

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, 2));

            long oldestRecord = plugin.Object.GetOldestRecordTotalSeconds(feature.Ref.ToString());
            Assert.AreEqual(1000, oldestRecord);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void HandleUpdateDeviceSettings()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            int deviceRefId = 373;
            var feature = TestHelper.SetupHsFeature(mockHsController, deviceRefId, 1.1, displayString: "1.1");

            Assert.IsTrue(plugin.Object.InitIO());

            Assert.IsTrue(plugin.Object.IsDeviceTracked(deviceRefId.ToString()));

            mockHsController.Setup(x => x.SaveINISetting(deviceRefId.ToString(), "DeviceRefId", deviceRefId.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceRefId.ToString(), "IsTracked", false.ToString(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.SaveINISetting(deviceRefId.ToString(), "RetentionPeriod", string.Empty, PlugInData.SettingFileName));

            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"refId\":\"373\",\"tracked\":0}", string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            Assert.IsFalse(jsonData.ContainsKey("error"));

            Assert.IsFalse(plugin.Object.IsDeviceTracked(deviceRefId.ToString()));

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        [TestMethod]
        public void HandleUpdateDeviceSettingsError()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            var feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1");

            Assert.IsTrue(plugin.Object.InitIO());

            // send invalid json
            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"tracked\":1}", string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsNotNull(errorMessage);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }

        private class StringValueComparer : IComparer<RecordData>
        {
            public int Compare(RecordData x, RecordData y) => StringComparer.Ordinal.Compare(x.DeviceString, y.DeviceString);
        }

        private class TimeComparer : IComparer<RecordData>
        {
            public int Compare(RecordData x, RecordData y) => Comparer<long>.Default.Compare(x.UnixTimeSeconds, y.UnixTimeSeconds);
        }

        private class ValueComparer : IComparer<RecordData>
        {
            public int Compare(RecordData x, RecordData y) => Comparer<double>.Default.Compare(x.DeviceValue, y.DeviceValue);
        }
    }
}