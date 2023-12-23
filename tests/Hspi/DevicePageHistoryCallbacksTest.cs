using System;
using System.Collections.Generic;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class DevicePageHistoryCallbacksTest
    {
        public static IEnumerable<object[]> GetDatatableCallbacksData()
        {
            // 1) min=0, max= lonf.max start = 0, length = 10, no ordering specified
            yield return new object[] {
                new  Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, _) => $"refId={feature.Ref}&min=0&max={long.MaxValue}&start=0&length=10"),
                new Func<List<RecordDataAndDuration>, List<RecordDataAndDuration>>( (records) => {
                    records.Sort(new TimeComparer());
                    records.Reverse();
                    return records.Take(10).ToList();
                } )
            };

            // 2) min , max, start = 0, length = 10, time sort ,desc
            yield return new object[] {
                new Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[10].UnixTimeMilliSeconds;
                    var max = records[30].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=0&length=10&order[0][column]=0&order[0][dir]=desc";
                }),
                new Func<List<RecordDataAndDuration>, List<RecordDataAndDuration>>( (records) => {
                    records.Sort(new TimeComparer());
                    return records.Skip(10).Take(30 - 10 + 1).Reverse().Take(10).ToList();
                } )
            };

            // 3) min , max, start = 10, length = 100, value sort ,desc
            yield return new object[] {
                new Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[80].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=100&order[0][column]=1&order[0][dir]=desc";
                }),
                new Func<List<RecordDataAndDuration>, List<RecordDataAndDuration>>( (records) => {
                    records.Sort(new TimeComparer());
                    var newList = records.Skip(20).Take(80 - 20 + 1).ToList();
                    newList.Sort(new ValueComparer());
                    newList.Reverse();
                    return newList.Skip(10).Take(100).ToList();
                } )
             };

            // 4) min , max, start = 10, length = 100, string sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[80].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=100&order[0][column]=2&order[0][dir]=asc";
                }),
                new Func<List<RecordDataAndDuration>, List<RecordDataAndDuration>>( (records) => {
                    records.Sort(new TimeComparer());
                    var newList = records.Skip(20).Take(80 - 20 + 1).ToList();
                    newList.Sort(new StringValueComparer());
                    return newList.Skip(10).Take(100).ToList();
                } )
             };

            // 5) min , max, start = 10, length = 10, string sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[30].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=10&order[0][column]=2&order[0][dir]=asc";
                }),
                new Func<List<RecordDataAndDuration>, List<RecordDataAndDuration>>( (records) => {
                    records.Sort(new TimeComparer());
                    var newList = records.Skip(20).Take(30 - 20 + 1).ToList();
                    newList.Sort(new StringValueComparer());
                    return newList.Skip(10).Take(10).ToList();
                } )
             };

            // 6) min , max, start = 10, length = 10, duration sort ,asc
            yield return new object[] {
                new Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[30].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=10&order[0][column]=3&order[0][dir]=asc";
                }),
                new Func<List<RecordDataAndDuration>, List<RecordDataAndDuration>>( (records) => {
                    records.Sort(new TimeComparer());
                    var newList = records.Skip(20).Take(30 - 20 + 1).ToList();

                    newList.Sort(new DurationComparer());
                    return newList.Skip(10).Take(10).ToList();
                } )
             };
        }

        public static IEnumerable<object[]> GetDatatableCallbackTotalData()
        {
            // 1) min , max, start = 0, length = 10, time sort ,desc
            yield return new object[] {
                new Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, records) =>
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
                new Func<HsFeature, List<RecordDataAndDuration>, string> ((feature, records) =>
                {
                    records.Sort(new TimeComparer());
                    var min = records[20].UnixTimeMilliSeconds;
                    var max = records[80].UnixTimeMilliSeconds;
                    return $"refId={feature.Ref}&min={min}&max={max}&start=10&length=100&order[0][column]=1&order[0][dir]=desc";
                }),
                100, 61
             };
        }

        [Test]
        [TestCaseSource(nameof(GetDatatableCallbacksData))]
        public void DatatableCallbackDataCorrect(Func<HsFeature, List<RecordDataAndDuration>, string> createString,
                                                 Func<List<RecordDataAndDuration>,
                                                 List<RecordDataAndDuration>> filter)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = TestHelper.SetUpMockSystemClockForCurrentTime(plugin);

            int refId = 948;
            mockHsController.SetupFeature(refId, 1.1, displayString: "1.1", lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var durations = new SortedDictionary<long, long?>();
            var added = new List<RecordDataAndDuration>();

            var existing = new List<double>();
            for (int i = 0; i < 100; i++)
            {
                double val = GetUniqueRandom(existing);
                DateTime lastChange = nowTime.AddMinutes(i * i);
                var featureLastTime = (DateTime)mockHsController.GetFeatureValue(refId, EProperty.LastChange);
                durations[((DateTimeOffset)featureLastTime).ToUnixTimeSeconds()] =
                                (long)(lastChange - featureLastTime).TotalSeconds;

                RecordData recordData = TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                                         refId, val, val.ToString(), lastChange, i + 1);

                long duration = (long)(lastChange - nowTime.AddMinutes((i - 1) * (i - 1))).TotalSeconds;
                added.Add(new RecordDataAndDuration(recordData.DeviceRefId, recordData.DeviceValue, recordData.DeviceString,
                                                    recordData.UnixTimeSeconds, duration));
            }

            var feature = mockHsController.GetFeature(refId);
            durations[((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds()] = null;

            string paramsForRecord = createString(feature, added.Clone());
            var records = TestHelper.GetHistoryRecords(plugin, refId, paramsForRecord);
            Assert.That(records, Is.Not.Null);

            var filterRecords = filter(added.Clone());

            Assert.That(filterRecords.Count, Is.EqualTo(records.Count));

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var expected = filterRecords[i];

                Assert.That(expected.DeviceRefId, Is.EqualTo(record.DeviceRefId));
                Assert.That(expected.UnixTimeSeconds, Is.EqualTo(record.UnixTimeSeconds));
                Assert.That(expected.DeviceValue, Is.EqualTo(record.DeviceValue));
                Assert.That(expected.DeviceString, Is.EqualTo(record.DeviceString));
                Assert.That(durations[record.UnixTimeSeconds], Is.EqualTo(record.DurationSeconds));
            }
        }

        [TestCase("refId={0}&min=1001&max=99&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "max is less than min")]
        [TestCase("refId={0}&min=abc&max=99&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "min is invalid")]
        [TestCase("refId={0}&start=10&length=100&order[0][column]=1&order[0][dir]=desc", "min or max not specified")]
        public void DatatableCallbackMinMoreThanMax(string format, string exception)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            int devRefId = 1938;
            string paramsForRecord = String.Format(format, devRefId);

            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.That(!string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, exception);
        }

        [Test]
        [TestCaseSource(nameof(GetDatatableCallbackTotalData))]
        public void DatatableCallbackTotalCorrect(Func<HsFeature, List<RecordDataAndDuration>, string> createString,
                                                  int addedRecordCount, int total)
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            DateTime nowTime = DateTime.Now;

            int refId = 373;
            mockHsController.SetupFeature(refId, 0, lastChange: nowTime);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            var added = new List<RecordDataAndDuration>();
            for (int i = 0; i < addedRecordCount; i++)
            {
                double val = 1000 - i;
                RecordData recordData = TestHelper.RaiseHSEventAndWait(plugin, mockHsController, Constants.HSEvent.VALUE_CHANGE,
                                                                         refId, val, val.ToString(), nowTime.AddMinutes(i), i + 1);
                long duration = (long)(nowTime.AddMinutes(i) - nowTime.AddMinutes(i - 1)).TotalSeconds;
                added.Add(new RecordDataAndDuration(recordData.DeviceRefId, recordData.DeviceValue, recordData.DeviceString,
                                                    recordData.UnixTimeSeconds, duration));
            }

            string paramsForRecord = createString(mockHsController.GetFeature(refId), added.Clone());

            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var recordsTotal = jsonData["recordsTotal"].Value<long>();
            Assert.That(recordsTotal, Is.EqualTo(total));

            var recordsFiltered = jsonData["recordsFiltered"].Value<long>();
            Assert.That(recordsFiltered, Is.EqualTo(total));
        }

        [Test]
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
            Assert.That(records[0], Is.EqualTo(2000));
            Assert.That(records[1], Is.EqualTo(100));
        }

        [Test]
        public void HandleUpdateDeviceSettings()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int deviceRefId = 373;
            mockHsController.SetupFeature(deviceRefId, 1.1);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1);

            Assert.That(plugin.Object.IsFeatureTracked(deviceRefId));

            mockHsController.SetupIniValue(deviceRefId.ToString(), "RefId", deviceRefId.ToString());
            mockHsController.SetupIniValue(deviceRefId.ToString(), "IsTracked", false.ToString());
            mockHsController.SetupIniValue(deviceRefId.ToString(), "MinValue", string.Empty);

            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"refId\":\"373\",\"tracked\":0, \"minValue\":10, \"maxValue\":20}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            Assert.That(!jsonData.ContainsKey("error"));

            Assert.That(!plugin.Object.IsFeatureTracked(deviceRefId));

            var list = plugin.Object.GetDevicePageHeaderStats(deviceRefId);
            Assert.That(list[5], Is.EqualTo(10D));
            Assert.That(list[6], Is.EqualTo(20D));

            // wait till all invalid records are deleted
            TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 0);
        }

        [Test]
        public void HandleUpdateDeviceSettingsError()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            mockHsController.SetupFeature(373, 1.1);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            // send invalid json
            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"tracked\":1}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void HandleUpdateDeviceSettingsNoMinMax()
        {
            TestHelper.CreateMockPlugInAndHsController2(out var plugin, out var mockHsController);

            int deviceRefId = 373;
            mockHsController.SetupFeature(deviceRefId, 1.1);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1);

            string data = plugin.Object.PostBackProc("updatedevicesettings", "{\"refId\":\"373\",\"tracked\":0, \"minValue\":null, \"maxValue\":null}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);
            Assert.That(!jsonData.ContainsKey("error"));

            Assert.That(!plugin.Object.IsFeatureTracked(deviceRefId));

            var list = plugin.Object.GetDevicePageHeaderStats(deviceRefId);
            Assert.That(list[5], Is.EqualTo(null));
            Assert.That(list[6], Is.EqualTo(null));
        }

        private static double GetUniqueRandom(List<double> existing, int max = 100000)
        {
            double val = rand.Next(max);

            while (existing.Contains(val))
            {
                val = rand.Next(max);
            }

            existing.Add(val);

            return val;
        }

        private class StringValueComparer : IComparer<RecordDataAndDuration>
        {
            public int Compare(RecordDataAndDuration x, RecordDataAndDuration y) => StringComparer.Ordinal.Compare(x.DeviceString, y.DeviceString);
        }

        private class TimeComparer : IComparer<RecordDataAndDuration>
        {
            public int Compare(RecordDataAndDuration x, RecordDataAndDuration y) => Comparer<long>.Default.Compare(x.UnixTimeSeconds, y.UnixTimeSeconds);
        }

        private class ValueComparer : IComparer<RecordDataAndDuration>
        {
            public int Compare(RecordDataAndDuration x, RecordDataAndDuration y) => Comparer<double>.Default.Compare(x.DeviceValue, y.DeviceValue);
        }

        private class DurationComparer : IComparer<RecordDataAndDuration>
        {
            public int Compare(RecordDataAndDuration x, RecordDataAndDuration y) => Comparer<long?>.Default.Compare(x.DurationSeconds, y.DurationSeconds);
        }

        private static readonly Random rand = new();
    }
}