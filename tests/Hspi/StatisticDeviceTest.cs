using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Hspi.Device;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class StatisticsDeviceTest
    {
        [TestCase("{\"name\":\"dev name\", \"data\": {\"StatisticsFunction\":3,\"FunctionDurationSeconds\":0,\"RefreshIntervalSeconds\":10}}", "Required property 'TrackedRef' not found")]
        [TestCase("", "data is not correct")]
        public void AddDeviceErrorChecking(string format, string exception)
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            //add
            string data = plugIn.Object.PostBackProc("devicecreate", format, string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.That(!string.IsNullOrWhiteSpace(errorMessage));
            Assert.That(errorMessage, Does.Contain(exception));
        }

        [Test]
        public void AddPastIntervalDevice([Values(null, 1928345)] int? parentRefId,
                                          [Values] StatisticsFunction function)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            if (parentRefId != null)
            {
                TestHelper.SetupStatisticsFeature(StatisticsFunction.MinimumValue, plugIn, hsControllerMock,
                     new DateTime(2000, 3, 4), parentRefId.Value, 999, 9999);
            }

            int trackedRefId = 1039423;
            SetUpFeatureWithLocationAndGraphics(hsControllerMock, trackedRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            string deviceName = "ssdfsd";
            long durationInterval = (long)new TimeSpan(1, 0, 0, 0).TotalSeconds;
            long refreshInterval = (long)new TimeSpan(0, 0, 1, 30).TotalSeconds;
            JObject request = CreateJsonForNewDevice(parentRefId, function, trackedRefId,
                                                     deviceName, durationInterval, refreshInterval);

            //add
            string createResultJson = plugIn.Object.PostBackProc("devicecreate", request.ToString(), string.Empty, 0);

            StatisticsDeviceData data = ValidateNewDevice(hsControllerMock, parentRefId, function,
                                                          trackedRefId, deviceName, createResultJson);

            Assert.That(data.StatisticsFunction, Is.EqualTo(function));
            Assert.That(data.StatisticsFunctionDuration.PreDefinedPeriod, Is.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.Start, Is.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.End, Is.Not.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.End.Type, Is.EqualTo(InstantType.Now));
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.End.Offsets, Is.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.FunctionDurationSeconds, Is.EqualTo(durationInterval));
            Assert.That(data.RefreshIntervalSeconds, Is.EqualTo(refreshInterval));

            int featureId = hsControllerMock.CreatedFeatures.First().Key;
            ValidateJsonForStatisticalFeature(plugIn, hsControllerMock, featureId,
                                             JsonConvert.DeserializeObject<StatisticsDeviceData>(request["data"].ToString()));
        }

        [Test]
        public void AddPredefinedPeriodDevice([Values(null, 2323422)] int? parentRefId,
                                              [Values] PreDefinedPeriod preDefinedPeriod,
                                              [Values] StatisticsFunction function)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            if (parentRefId != null)
            {
                TestHelper.SetupStatisticsFeature(StatisticsFunction.MinimumValue, plugIn, hsControllerMock,
                     new DateTime(2000, 3, 4), parentRefId.Value, 999, 9999);
            }

            int trackedRefId = 1039423;
            SetUpFeatureWithLocationAndGraphics(hsControllerMock, trackedRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            string deviceName = "A New Device";
            long refreshInterval = (long)new TimeSpan(3, 8, 1, 30).TotalSeconds;
            JObject request = CreateJsonForNewDevice(parentRefId, function, trackedRefId,
                                                     deviceName, preDefinedPeriod, refreshInterval);

            //add
            string data2 = plugIn.Object.PostBackProc("devicecreate", request.ToString(), string.Empty, 0);

            StatisticsDeviceData data = ValidateNewDevice(hsControllerMock, parentRefId, function, trackedRefId, deviceName, data2);

            Assert.That(data.StatisticsFunction, Is.EqualTo(function));
            Assert.That(data.StatisticsFunctionDuration.PreDefinedPeriod, Is.EqualTo(preDefinedPeriod));
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod, Is.Null);
            Assert.That(data.RefreshIntervalSeconds, Is.EqualTo(refreshInterval));

            int featureId = hsControllerMock.CreatedFeatures.First().Key;
            ValidateJsonForStatisticalFeature(plugIn, hsControllerMock, featureId,
                                             JsonConvert.DeserializeObject<StatisticsDeviceData>(request["data"].ToString()));
        }

        [Test]
        public void DecimalPlaceCalculatedProperlyForStatDevice()
        {
            int trackedRefId = 1039423;
            var function = StatisticsFunction.AverageStep;
            var preDefinedPeriod = PreDefinedPeriod.Today;

            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            hsControllerMock.SetupFeature(trackedRefId, 10.03847264, "10.03847264 A");
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.Name, "A Unique Device1");

            var collection = new StatusGraphicCollection();
            collection.Add(new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 0 }));
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.StatusGraphics, collection);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            string deviceName = "A New Device";
            long refreshInterval = (long)new TimeSpan(3, 8, 1, 30).TotalSeconds;
            JObject request = CreateJsonForNewDevice(null, function, trackedRefId,
                                                     deviceName, preDefinedPeriod, refreshInterval);

            //add
            string data2 = plugIn.Object.PostBackProc("devicecreate", request.ToString(), string.Empty, 0);

            NewFeatureData newFeatureData = hsControllerMock.CreatedFeatures.First().Value;
            Assert.That(newFeatureData, Is.Not.Null);

            var list2 = ((StatusGraphicCollection)newFeatureData.Feature[EProperty.StatusGraphics]).Values;
            Assert.That(list2.Count, Is.EqualTo(1));
            Assert.That(list2[0].TargetRange.DecimalPlaces, Is.EqualTo(8));
        }

        [Test]
        public void DeviceIsUpdated([Values] StatisticsFunction statisticsFunction)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTimeOffset aTime = new(2222, 2, 2, 2, 2, 2, TimeSpan.FromHours(-11));

            int statsDeviceRefId = 100;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 10;
            TestHelper.SetupStatisticsFeature(statisticsFunction, plugIn, hsControllerMock, aTime,
                                             statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.WaitForRecordCountAndDeleteAll(plugIn, trackedDeviceRefId, 1);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 10, "10", aTime.AddMinutes(-10).LocalDateTime, 1);
            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 10, "10", aTime.AddMinutes(-9).LocalDateTime, 2);
            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 50, "50", aTime.AddMinutes(-5).LocalDateTime, 3);

            Assert.That(plugIn.Object.UpdateStatisticsFeature(statsFeatureRefId));

            double expectedValue = 0;
            switch (statisticsFunction)
            {
                case StatisticsFunction.AverageStep:
                    expectedValue = ((int)(1000 * ((10D * 5 * 61) + (50D * 5 * 60)) / 601D)) / 1000D; break;
                case StatisticsFunction.AverageLinear:
                    const double value = ((10D * 1 * 60) + (30D * 4 * 60) + (50D * 301)) / 601D;
                    expectedValue = Math.Round(value, 3); break;
                case StatisticsFunction.MinimumValue:
                    expectedValue = 10D; break;
                case StatisticsFunction.MaximumValue:
                    expectedValue = 50D; break;
                case StatisticsFunction.DistanceBetweenMinAndMax:
                    expectedValue = 40D; break;
                case StatisticsFunction.RecordsCount:
                    expectedValue = 3D; break;
                case StatisticsFunction.ValueChangedCount:
                    expectedValue = 2D; break;
                case StatisticsFunction.LinearRegression:
                    expectedValue = 8.571D; break;
                case StatisticsFunction.Difference:
                    expectedValue = 40D; break;

                default:
                    Assert.Fail();
                    break;
            }

            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId, expectedValue);

            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.InvalidValue), Is.EqualTo(false));
        }

        [Test]
        public void DifferenceDeviceIsUpdatedIfNoValueChange()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTimeOffset aTime = new(2222, 2, 2, 2, 2, 2, TimeSpan.FromHours(-11));

            int statsDeviceRefId = 100;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 10;
            TestHelper.SetupStatisticsFeature(StatisticsFunction.Difference, plugIn, hsControllerMock, aTime,
                                             statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.WaitForRecordCountAndDeleteAll(plugIn, trackedDeviceRefId, 1);

            // add a really old record, older than 10 mins
            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 10, "10", aTime.AddMinutes(-100).LocalDateTime, 1);

            Assert.That(plugIn.Object.UpdateStatisticsFeature(statsFeatureRefId));

            double expectedValue = 0;

            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId, expectedValue);

            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.InvalidValue), Is.EqualTo(false));
        }

        [Test]
        public void DeviceIsUpdatedRounded()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTimeOffset aTime = new(2222, 2, 2, 2, 2, 2, TimeSpan.FromHours(-2));

            int statsDeviceRefId = 100;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 99;

            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageStep, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            hsControllerMock.SetupDevOrFeatureValue(trackedDeviceRefId, EProperty.StatusGraphics, statusGraphics);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.WaitForRecordCountAndDeleteAll(plugIn, trackedDeviceRefId, 1);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 11.85733, "11.2", aTime.AddMinutes(-10).LocalDateTime, 1);

            plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId);

            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId, 11.9D);

            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.InvalidValue), Is.EqualTo(false));
        }

        [Test]
        public void DevicePolled([Values] bool device)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 100;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 99;

            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageStep, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            hsControllerMock.SetupDevOrFeatureValue(trackedDeviceRefId, EProperty.StatusGraphics, statusGraphics);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.WaitForRecordCountAndDeleteAll(plugIn, trackedDeviceRefId, 1);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 11.85733, "11.2", aTime.AddMinutes(-10), 1);

            Assert.That(plugIn.Object.UpdateStatusNow(device ? statsDeviceRefId : statsFeatureRefId), Is.EqualTo(EPollResponse.Ok));

            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId, 11.9D);

            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.InvalidValue), Is.EqualTo(false));
        }

        [Test]
        public void EditDeviceFailsForInvalidDevice()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 199;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 100;

            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageLinear, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            long durationInterval = (long)new TimeSpan(1, 3, 10, 0).TotalSeconds;
            long refreshInterval = (long)new TimeSpan(3, 8, 1, 30).TotalSeconds;

            JObject editRequest = new()
            {
                { "ref" , new JValue(trackedDeviceRefId) }, // wrong ref
                { "data" , TestHelper.CreateJsonForDevice(StatisticsFunction.MaximumValue, trackedDeviceRefId, durationInterval, refreshInterval)}
            };

            // edit
            string data2 = plugIn.Object.PostBackProc("deviceedit", editRequest.ToString(), string.Empty, 0);

            // error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);
            Assert.That(result2, Is.Not.Null);
            StringAssert.Contains((string)result2["error"], $"Device or feature {trackedDeviceRefId} not a plugin feature");
        }

        [Test]
        public void EditDeviceForLastInterval([Values] StatisticsFunction function)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 100;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 10;

            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageLinear, plugIn, hsControllerMock, aTime,
                                              statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            long durationInterval = (long)new TimeSpan(1, 3, 10, 0).TotalSeconds;
            long refreshInterval = (long)new TimeSpan(3, 8, 1, 30).TotalSeconds;

            JObject editRequest = new()
            {
                { "ref", new JValue(statsFeatureRefId) },
                { "data",TestHelper.CreateJsonForDevice(function, trackedDeviceRefId, durationInterval, refreshInterval) },
            };

            // edit
            string data2 = plugIn.Object.PostBackProc("deviceedit", editRequest.ToString(), string.Empty, 0);

            // no error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);

            Assert.That(result2, Is.Not.Null);
            Assert.That((string)result2["error"], Is.Null);

            ValidateJsonForStatisticalFeature(plugIn, hsControllerMock, statsFeatureRefId,
                                             JsonConvert.DeserializeObject<StatisticsDeviceData>(editRequest["data"].ToString()));
        }

        [Test]
        public void EditDeviceForPredefinePeriods([Values] PreDefinedPeriod preDefinedPeriod, [Values] StatisticsFunction function)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 100;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 10;

            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageLinear, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            long refreshInterval = (long)new TimeSpan(3, 8, 1, 30).TotalSeconds;

            JObject editRequest = new()
            {
                { "ref", new JValue(statsFeatureRefId) },
                { "data",TestHelper.CreateJsonForDevice(function, trackedDeviceRefId, preDefinedPeriod, refreshInterval) },
            };

            // edit
            string data2 = plugIn.Object.PostBackProc("deviceedit", editRequest.ToString(), string.Empty, 0);

            // no error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);

            Assert.That(result2, Is.Not.Null);
            Assert.That((string)result2["error"], Is.Null);

            ValidateJsonForStatisticalFeature(plugIn, hsControllerMock, statsFeatureRefId,
                                             JsonConvert.DeserializeObject<StatisticsDeviceData>(editRequest["data"].ToString()));
        }

        [Test]
        public void MultipleFeatureInDeviceAreUpdated()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 100;
            int statsFeatureRefId1 = 1000;
            int statsFeatureRefId2 = 1001;
            int trackedDeviceRefId1 = 99;
            int trackedDeviceRefId2 = 100023;

            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageStep, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, statsFeatureRefId1, trackedDeviceRefId1);
            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageStep, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, statsFeatureRefId2, trackedDeviceRefId2);

            hsControllerMock.SetupDevOrFeatureValue(statsDeviceRefId, EProperty.AssociatedDevices, new HashSet<int> { statsFeatureRefId1, statsFeatureRefId2 });

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.WaitForRecordCountAndDeleteAll(plugIn, trackedDeviceRefId1, 1);
            TestHelper.WaitForRecordCountAndDeleteAll(plugIn, trackedDeviceRefId2, 1);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId1, 10, "10", aTime.AddMinutes(-10), 1);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId2, 100, "100", aTime.AddMinutes(-10), 1);

            plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId);

            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId1, 10);
            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId2, 100);

            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId1, EProperty.InvalidValue), Is.EqualTo(false));
            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId2, EProperty.InvalidValue), Is.EqualTo(false));
        }

        [Test]
        public void StatisticsDeviceIsDeleted()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 1080;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 100;

            TestHelper.SetupStatisticsFeature(StatisticsFunction.AverageStep, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            Assert.That(TestHelper.TimedWaitTillTrue(() => plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId)));

            Assert.That(hsControllerMock.RemoveFeatureOrDevice(statsDeviceRefId));
            Assert.That(hsControllerMock.RemoveFeatureOrDevice(statsFeatureRefId));
            plugIn.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE,
                                  new object[] { null, null, null, statsDeviceRefId, 2 });

            Assert.That(TestHelper.TimedWaitTillTrue(() => !plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId)));

            // not more tracking after delete
            Assert.That(!plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId));
            Assert.That(!plugIn.Object.UpdateStatisticsFeature(statsFeatureRefId));
        }

        private static JObject CreateJsonForNewDevice(int? parentRefId, StatisticsFunction function,
                                                      int trackedRefId, string deviceName,
                                                      long durationInterval, long refreshInterval)
        {
            JObject request = new()
            {
                { "name", new JValue(deviceName) },
                { "parentRef", new JValue(parentRefId) },
                { "data", TestHelper.CreateJsonForDevice(function, trackedRefId, durationInterval, refreshInterval) },
            };
            return request;
        }

        private static JObject CreateJsonForNewDevice(int? parentRefId, StatisticsFunction function,
                                                      int trackedRefId, string deviceName,
                                                      PreDefinedPeriod preDefinedPeriod,
                                                      long refreshInterval)
        {
            JObject request = new()
            {
                { "name", new JValue(deviceName) },
                { "parentRef", new JValue(parentRefId) },
                { "data", TestHelper.CreateJsonForDevice(function, trackedRefId, preDefinedPeriod, refreshInterval) },
            };
            return request;
        }

        private static void SetUpFeatureWithLocationAndGraphics(FakeHSController hsControllerMock, int trackedRefId)
        {
            hsControllerMock.SetupFeature(trackedRefId, 1.132, "10 A");

            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.Name, "A Unique Device1");
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.Location, "Loc1");
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.Location2, "Loc1");
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.AdditionalStatusData, new List<string> { "A" });

            var collection = new StatusGraphicCollection();
            collection.Add(new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 2 }));
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.StatusGraphics, collection);
        }

        private static void ValidateJsonForStatisticalFeature(Mock<PlugIn> plugIn,
                                                              FakeHSController hsControllerMock,
                                                              int statsFeatureRefId,
                                                              StatisticsDeviceData expected)
        {
            // get return jsons for feature
            var jsons = plugIn.Object.GetStatisticDeviceDataAsJson(statsFeatureRefId);

            Assert.That(JsonConvert.DeserializeObject<StatisticsDeviceData>(jsons[statsFeatureRefId]),
                        Is.EqualTo(expected));

            var plugExtraData = (PlugExtraData)hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.PlugExtraData);
            Assert.That(plugExtraData.NamedKeys.Count, Is.EqualTo(1));
            Assert.That(JsonConvert.DeserializeObject<StatisticsDeviceData>(jsons[statsFeatureRefId]),
                        Is.EqualTo(JsonConvert.DeserializeObject<StatisticsDeviceData>(plugExtraData["data"])));
        }

        private static StatisticsDeviceData ValidateNewDevice(FakeHSController hsControllerMock, int? parentRefId,
                                                              StatisticsFunction function, int trackedRefId,
                                                              string deviceName, string createResultJson)
        {
            var result2 = JsonConvert.DeserializeObject<JObject>(createResultJson);
            Assert.That(result2, Is.Not.Null);
            Assert.That((string)result2["error"], Is.Null);

            var trackedFeature = hsControllerMock.GetFeature(trackedRefId);

            Assert.That(hsControllerMock.CreatedFeatures.Count, Is.EqualTo(1));

            NewFeatureData newFeatureData = hsControllerMock.CreatedFeatures.First().Value;
            if (parentRefId is null)
            {
                Assert.That(hsControllerMock.CreatedDevices.Count, Is.EqualTo(1));
                NewDeviceData newDataForDevice = hsControllerMock.CreatedDevices.First().Value;

                // check proper device & feature was added
                Assert.That(newDataForDevice, Is.Not.Null);

                Assert.That(deviceName, Is.EqualTo(((string)newDataForDevice.Device[EProperty.Name])));
                Assert.That(newDataForDevice.Device[EProperty.Interface], Is.EqualTo(PlugInData.PlugInId));
                Assert.That(newDataForDevice.Device[EProperty.Location], Is.EqualTo(trackedFeature.Location));
                Assert.That(newDataForDevice.Device[EProperty.Location2], Is.EqualTo(trackedFeature.Location2));

                CollectionAssert.AreEqual((new HashSet<int> { hsControllerMock.CreatedDevices.First().Key }).ToImmutableArray(),
                                         ((HashSet<int>)newFeatureData.Feature[EProperty.AssociatedDevices]).ToImmutableArray());
            }
            else
            {
                Assert.That(hsControllerMock.CreatedDevices.Count, Is.EqualTo(0));
                CollectionAssert.AreEqual((new HashSet<int> { parentRefId.Value }).ToImmutableArray(),
                                         ((HashSet<int>)newFeatureData.Feature[EProperty.AssociatedDevices]).ToImmutableArray());
            }

            Assert.That(newFeatureData.Feature[EProperty.Interface], Is.EqualTo(PlugInData.PlugInId));
            if (function is StatisticsFunction.LinearRegression)
            {
                CollectionAssert.AreEqual((List<string>)newFeatureData.Feature[EProperty.AdditionalStatusData], new List<string> { "A per minute" });
            }
            else if (function is not StatisticsFunction.RecordsCount and not StatisticsFunction.ValueChangedCount)
            {
                CollectionAssert.AreEqual(trackedFeature.AdditionalStatusData, (List<string>)newFeatureData.Feature[EProperty.AdditionalStatusData]);
            }

            Assert.That(newFeatureData.Feature[EProperty.Location], Is.EqualTo(trackedFeature.Location));
            Assert.That(newFeatureData.Feature[EProperty.Location2], Is.EqualTo(trackedFeature.Location2));
            Assert.That(newFeatureData.Feature[EProperty.Misc],
                        Is.EqualTo((uint)(EMiscFlag.StatusOnly | EMiscFlag.SetDoesNotChangeLastChange | EMiscFlag.ShowValues)));

            var plugExtraData = (PlugExtraData)newFeatureData.Feature[EProperty.PlugExtraData];

            Assert.That(plugExtraData.NamedKeys.Count, Is.EqualTo(1));

            var data = JsonConvert.DeserializeObject<StatisticsDeviceData>(plugExtraData["data"]);

            Assert.That(data.TrackedRef, Is.EqualTo(trackedFeature.Ref));

            switch (function)
            {
                case StatisticsFunction.AverageStep:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Average(Step)"));
                    break;

                case StatisticsFunction.AverageLinear:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Average(Linear)"));
                    break;

                case StatisticsFunction.MinimumValue:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Minimum Value"));
                    break;

                case StatisticsFunction.MaximumValue:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Maximum Value"));
                    break;

                case StatisticsFunction.DistanceBetweenMinAndMax:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Distance Min-Max Value"));
                    break;

                case StatisticsFunction.RecordsCount:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Count"));
                    break;

                case StatisticsFunction.ValueChangedCount:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Value Changed Count"));
                    break;

                case StatisticsFunction.LinearRegression:
                    Assert.That(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Slope"));
                    break;
            }

            switch (function)
            {
                default:
                    {
                        var list1 = trackedFeature.StatusGraphics.Values;
                        var list2 = ((StatusGraphicCollection)newFeatureData.Feature[EProperty.StatusGraphics]).Values;
                        Assert.That(list1.Count, Is.EqualTo(1));
                        Assert.That(list2.Count, Is.EqualTo(1));
                        Assert.That(list2[0].Label, Is.EqualTo(list1[0].Label));
                        Assert.That(list2[0].IsRange, Is.EqualTo(list1[0].IsRange));
                        Assert.That(list2[0].ControlUse, Is.EqualTo(list1[0].ControlUse));
                        Assert.That(list2[0].HasAdditionalData, Is.EqualTo(list1[0].HasAdditionalData));
                        Assert.That(list2[0].TargetRange, Is.EqualTo(list1[0].TargetRange));
                        break;
                    }
                case StatisticsFunction.LinearRegression:
                    {
                        var list2 = ((StatusGraphicCollection)newFeatureData.Feature[EProperty.StatusGraphics]).Values;
                        Assert.That(list2.Count, Is.EqualTo(1));
                        Assert.That(list2[0].IsRange, Is.True);
                        Assert.That(list2[0].TargetRange.Min, Is.EqualTo(int.MinValue));
                        Assert.That(list2[0].TargetRange.Max, Is.EqualTo(int.MaxValue));
                        Assert.That(list2[0].TargetRange.DecimalPlaces, Is.EqualTo(5));
                        Assert.That(list2[0].TargetRange.Suffix, Is.EqualTo(" $%0$"));
                        break;
                    }

                case StatisticsFunction.RecordsCount:
                case StatisticsFunction.ValueChangedCount:
                    {
                        var list2 = ((StatusGraphicCollection)newFeatureData.Feature[EProperty.StatusGraphics]).Values;
                        Assert.That(list2.Count, Is.EqualTo(1));
                        Assert.That(list2[0].IsRange, Is.True);
                        Assert.That(list2[0].TargetRange.Min, Is.EqualTo(int.MinValue));
                        Assert.That(list2[0].TargetRange.Max, Is.EqualTo(int.MaxValue));
                        Assert.That(list2[0].TargetRange.DecimalPlaces, Is.EqualTo(0));
                        break;
                    }
            }

            return data;
        }
    }
}