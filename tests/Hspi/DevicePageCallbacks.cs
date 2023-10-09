﻿using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        public void DatatableCallbackError()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            Assert.IsTrue(plugin.Object.InitIO());

            int devRefId = 1938;
            string paramsForRecord = $"refId={devRefId}&min={1001}&max={99}&start=10&length=100&order[0][column]=1&order[0][dir]=desc";

            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));

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

            var feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime,
                                               apiType: (int)EApiType.Feature);

            Assert.IsTrue(plugin.Object.InitIO());

            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                double val = 1000 - i;
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, Constants.HSEvent.VALUE_CHANGE,
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

            var feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime,
                                               apiType: (int)EApiType.Feature);

            Assert.IsTrue(plugin.Object.InitIO());

            var added = new List<RecordData>();
            for (int i = 0; i < 100; i++)
            {
                double val = 1000 - i;
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, Constants.HSEvent.VALUE_CHANGE,
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
        public void GetOldestRecordTimeDate()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            DateTime nowTime = new(2222, 2, 2, 2, 2, 2);

            var feature = TestHelper.SetupHsFeature(mockHsController, 373, 1.1, displayString: "1.1", lastChange: nowTime,
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