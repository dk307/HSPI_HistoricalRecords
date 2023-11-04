using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class DevicePageHistoryCallbacksTest
    {
        public static IEnumerable<object[]> GetDatatableCallbacksData()
        {
            // 1) min=0, max= lonf.max start = 0, length = 10, no ordering specified
            yield return new object[] {
                new  Func<HsFeature, List<RecordData>, string> ((feature, _) => $"refId={feature.Ref}&min=0&max={long.MaxValue}&start=0&length=10"),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new TimeComparer());
                    records.Reverse();
                    return records.Take(10).ToList();
                } )
            };

            // 2) min , max, start = 0, length = 10, time sort ,desc
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

            // 3) min , max, start = 10, length = 100, value sort ,desc
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

            // 4) min , max, start = 10, length = 100, string sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordData>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[80].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=100&order[0][column]=&2order[0][dir]=asc";
                }),
                new Func<List<RecordData>, List<RecordData>>( (records) => {
                    records.Sort(new TimeComparer());
                    var newList = records.Skip(20).Take(80 - 20 + 1).ToList();
                    newList.Sort(new StringValueComparer());
                    return newList.Skip(10).Take(100).ToList();
                } )
             };
        }

        public static IEnumerable<object[]> GetDatatableCallbackTotalData()
        {
            // 1) min , max, start = 0, length = 10, time sort ,desc
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

            // 2) min , max, start = 10, length = 100, value sort ,asc
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

        [DataTestMethod]
        [DataRow("refId={0}&min=1001&max=99&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "max < min")]
        [DataRow("refId={0}&min=abc&max=99&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "min is invalid")]
        [DataRow("refId={0}&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "min/max not specified")]
        public void DatatableCallbackMinMoreThanMax(string format, string exception)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devRefId = 1938;
            string paramsForRecord = String.Format(format, devRefId);

            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, exception);
        }

        [TestMethod]
        [DynamicData(nameof(GetDatatableCallbackTotalData), DynamicDataSourceType.Method)]
        public void DatatableCallbackTotalCorrect(Func<HsFeature, List<RecordData>, string> createString, int addedRecordCount, int total)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = DateTime.Now;

            int refId = 373;
            mockHsController.SetupFeature(refId, 0, lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var added = new List<RecordData>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                double val = 1000 - i;
                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         refId, val, val.ToString(), nowTime.AddMinutes(i), i + 1));
            }

            string paramsForRecord = createString(mockHsController.GetFeature(refId), added.Clone());

            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var recordsTotal = jsonData["recordsTotal"].Value<long>();
            Assert.AreEqual(total, recordsTotal);

            var recordsFiltered = jsonData["recordsFiltered"].Value<long>();
            Assert.AreEqual(total, recordsFiltered);
        }

        [TestMethod]
        [DynamicData(nameof(GetDatatableCallbacksData), DynamicDataSourceType.Method)]
        public void DatatableCallbackDataCorrect(Func<HsFeature, List<RecordData>, string> createString, Func<List<RecordData>, List<RecordData>> filter)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 948;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var durations = new SortedDictionary<long, long?>();
            var added = new List<RecordData>();
            for (int i = 0; i < 100; i++)
            {
                double val = 1000 - i;
                DateTime lastChange = nowTime.AddMinutes(i * i);
                var featureLastTime = (DateTime)mockHsController.GetFeatureValue(refId, EProperty.LastChange);
                durations[((DateTimeOffset)featureLastTime).ToUnixTimeSeconds()] =
                                (long)(lastChange - featureLastTime).TotalSeconds;

                added.Add(TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                         refId, val, val.ToString(), lastChange, i + 1));
            }

            var feature = mockHsController.GetFeature(refId);
            durations[((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds()] = null;

            string paramsForRecord = createString(feature, added.Clone());
            var records = TestHelper.GetHistoryRecords(plugin, refId, paramsForRecord);
            Assert.IsNotNull(records);

            var filterRecords = filter(added.Clone());

            Assert.AreEqual(records.Count, filterRecords.Count);

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var expected = filterRecords[i];

                Assert.AreEqual(record.DeviceRefId, expected.DeviceRefId);
                Assert.AreEqual(record.UnixTimeSeconds, expected.UnixTimeSeconds);
                Assert.AreEqual(record.DeviceValue, expected.DeviceValue);
                Assert.AreEqual(record.DeviceString, expected.DeviceString);
                Assert.AreEqual(record.DurationSeconds, durations[record.UnixTimeSeconds]);
            }
        }

        [TestMethod]
        public void GetEarliestAndOldestRecordTimeDate()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 42;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime.AddSeconds(-100));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE, refId, 3333, "3333", nowTime.AddSeconds(-100), 1);
            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE, refId, 33434, "333", nowTime.AddSeconds(-1000), 2);
            TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE, refId, 334, "333", nowTime.AddSeconds(-2000), 3);

            var records = plugin.Object.GetEarliestAndOldestRecordTotalSeconds(refId);
            Assert.AreEqual(2000, records[0]);
            Assert.AreEqual(100, records[1]);
        }

        [TestMethod]
        public void HandleUpdateDeviceSettings()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int deviceRefId = 373;
            mockHsController.SetupFeature(deviceRefId, 1.1);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1);

            Assert.IsTrue(plugin.Object.IsFeatureTracked(deviceRefId));

            mockHsController.SetupIniValue(deviceRefId.ToString(), "RefId", deviceRefId.ToString());
            mockHsController.SetupIniValue(deviceRefId.ToString(), "IsTracked", false.ToString());
            mockHsController.SetupIniValue(deviceRefId.ToString(), "MinValue", string.Empty);

            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"refId\":\"373\",\"tracked\":0, \"minValue\":10, \"maxValue\":20}", string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            Assert.IsFalse(jsonData.ContainsKey("error"));

            Assert.IsFalse(plugin.Object.IsFeatureTracked(deviceRefId));

            var list = plugin.Object.GetDevicePageHeaderStats(deviceRefId);
            Assert.AreEqual(10D, list[5]);
            Assert.AreEqual(20D, list[6]);

            // wait till all invalid records are deleted
            TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 0);
        }

        [TestMethod]
        public void HandleUpdateDeviceSettingsNoMinMax()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int deviceRefId = 373;
            mockHsController.SetupFeature(deviceRefId, 1.1);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1);

            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"refId\":\"373\",\"tracked\":0, \"minValue\":null, \"maxValue\":null}", string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);
            Assert.IsFalse(jsonData.ContainsKey("error"));

            Assert.IsFalse(plugin.Object.IsFeatureTracked(deviceRefId));

            var list = plugin.Object.GetDevicePageHeaderStats(deviceRefId);
            Assert.AreEqual(null, list[5]);
            Assert.AreEqual(null, list[6]);
        }

        [TestMethod]
        public void HandleUpdateDeviceSettingsError()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            mockHsController.SetupFeature(373, 1.1);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            // send invalid json
            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"tracked\":1}", string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsNotNull(errorMessage);
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