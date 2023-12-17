﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Hspi.Device;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class StatisticsDeviceTest
    {
        [TestCase(null, StatisticsFunction.AverageStep)]
        [TestCase(null, StatisticsFunction.AverageLinear)]
        [TestCase(null, StatisticsFunction.MinimumValue)]
        [TestCase(null, StatisticsFunction.MaximumValue)]
        [TestCase(10294234, StatisticsFunction.MaximumValue)]
        [TestCase(33323343, StatisticsFunction.AverageLinear)]
        [TestCase(90000235, StatisticsFunction.MinimumValue)]
        [TestCase(60000235, StatisticsFunction.MaximumValue)]
        public void AddDevice(int? parentRefId, StatisticsFunction function)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            if (parentRefId != null)
            {
                TestHelper.SetupStatisticsFeature(StatisticsFunction.MinimumValue, plugIn, hsControllerMock,
                     new DateTime(2000, 3, 4), parentRefId.Value, 999, 9999);
            }

            int trackedRefId = 1039423;
            hsControllerMock.SetupFeature(trackedRefId, 1.132);

            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.Name, "A Unique Device");
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.Location, "1 Loc");
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.Location2, "2 Loc");
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.AdditionalStatusData, new List<string> { "ad" });

            var collection = new StatusGraphicCollection();
            collection.Add(new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }));
            hsControllerMock.SetupDevOrFeatureValue(trackedRefId, EProperty.StatusGraphics, collection);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            string deviceName = "ssdfsd";
            long durationInterval = (long)new TimeSpan(1, 0, 0, 0).TotalSeconds;
            long refreshInterval = (long)new TimeSpan(0, 0, 1, 30).TotalSeconds;
            JObject request = CreateJsonForNewDevice(parentRefId, function, trackedRefId,
                                                  deviceName, durationInterval, refreshInterval);

            //add
            string data2 = plugIn.Object.PostBackProc("devicecreate", request.ToString(), string.Empty, 0);

            // check no error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);
            Assert.That(result2, Is.Not.Null);
            Assert.That((string)result2["error"], Is.Null);

            var trackedFeature = hsControllerMock.GetFeature(trackedRefId);
            NewFeatureData newFeatureData = hsControllerMock.CreatedFeatures.First().Value;
            if (parentRefId is null)
            {
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
                CollectionAssert.AreEqual((new HashSet<int> { parentRefId.Value }).ToImmutableArray(),
                                         ((HashSet<int>)newFeatureData.Feature[EProperty.AssociatedDevices]).ToImmutableArray());
            }

            Assert.That(newFeatureData.Feature[EProperty.Interface], Is.EqualTo(PlugInData.PlugInId));
            CollectionAssert.AreEqual(trackedFeature.AdditionalStatusData, (List<string>)newFeatureData.Feature[EProperty.AdditionalStatusData]);
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
            }

            Assert.That(data.StatisticsFunction, Is.EqualTo(function));
            Assert.That(data.StatisticsFunctionDuration.PreDefinedPeriod, Is.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.Start, Is.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.End, Is.Not.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.End.Type, Is.EqualTo(InstantType.Now));
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.End.Offsets, Is.Null);
            Assert.That(data.StatisticsFunctionDuration.CustomPeriod.FunctionDurationSeconds, Is.EqualTo(durationInterval));
            Assert.That(data.RefreshIntervalSeconds, Is.EqualTo(refreshInterval));

            var list1 = trackedFeature.StatusGraphics.Values;
            var list2 = ((StatusGraphicCollection)newFeatureData.Feature[EProperty.StatusGraphics]).Values;
            Assert.That(list1.Count, Is.EqualTo(1));
            Assert.That(list2.Count, Is.EqualTo(1));
            Assert.That(list2[0].Label, Is.EqualTo(list1[0].Label));
            Assert.That(list2[0].IsRange, Is.EqualTo(list1[0].IsRange));
            Assert.That(list2[0].ControlUse, Is.EqualTo(list1[0].ControlUse));
            Assert.That(list2[0].HasAdditionalData, Is.EqualTo(list1[0].HasAdditionalData));
            Assert.That(list2[0].TargetRange, Is.EqualTo(list1[0].TargetRange));
        }

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

        [TestCase(StatisticsFunction.AverageStep)]
        [TestCase(StatisticsFunction.AverageLinear)]
        [TestCase(StatisticsFunction.MinimumValue)]
        [TestCase(StatisticsFunction.MaximumValue)]
        public void DeviceIsUpdated(StatisticsFunction statisticsFunction)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock = TestHelper.SetupHsControllerAndSettings2(plugIn);

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 100;
            int statsFeatureRefId = 1000;
            int trackedDeviceRefId = 10;
            TestHelper.SetupStatisticsFeature(statisticsFunction, plugIn, hsControllerMock, aTime,
                                             statsDeviceRefId, statsFeatureRefId, trackedDeviceRefId);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.WaitForRecordCountAndDeleteAll(plugIn, trackedDeviceRefId, 1);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 10, "10", aTime.AddMinutes(-10), 1);
            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedDeviceRefId, 20, "20", aTime.AddMinutes(-5), 2);

            Assert.That(plugIn.Object.UpdateStatisticsFeature(statsFeatureRefId));

            double ExpectedValue = 0;
            switch (statisticsFunction)
            {
                case StatisticsFunction.AverageStep:
                    ExpectedValue = ((10D * 5 * 60) + (20D * 5 * 60)) / 600D; break;
                case StatisticsFunction.AverageLinear:
                    ExpectedValue = ((15D * 5 * 60) + (20D * 5 * 60)) / 600D; break;
                case StatisticsFunction.MinimumValue:
                    ExpectedValue = 10D; break;
                case StatisticsFunction.MaximumValue:
                    ExpectedValue = 20D; break;

                default:
                    Assert.Fail();
                    break;
            }

            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId, ExpectedValue);

            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.InvalidValue), Is.EqualTo(false));
        }

        [Test]
        public void DeviceIsUpdatedRounded()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings2(plugIn);

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

            plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId);

            TestHelper.WaitTillExpectedValue(hsControllerMock, statsFeatureRefId, 11.9D);

            Assert.That(hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.InvalidValue), Is.EqualTo(false));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DevicePolled(bool device)
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

        [TestCase(StatisticsFunction.AverageStep)]
        [TestCase(StatisticsFunction.AverageLinear)]
        public void EditDevice(StatisticsFunction function)
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
                { "data",TestHelper.CreateJsonForPastDuationDevice(function, trackedDeviceRefId, durationInterval, refreshInterval) },
            };

            // edit
            string data2 = plugIn.Object.PostBackProc("deviceedit", editRequest.ToString(), string.Empty, 0);

            // no error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);

            Assert.That(result2, Is.Not.Null);
            Assert.That((string)result2["error"], Is.Null);

            // get return function value for feature
            var jsons = plugIn.Object.GetStatisticDeviceDataAsJson(statsFeatureRefId);

            Assert.That(JsonConvert.DeserializeObject<StatisticsDeviceData>(jsons[statsFeatureRefId]),

                            Is.EqualTo(JsonConvert.DeserializeObject<StatisticsDeviceData>(editRequest["data"].ToString())));

            var plugExtraData = (PlugExtraData)hsControllerMock.GetFeatureValue(statsFeatureRefId, EProperty.PlugExtraData);
            Assert.That(plugExtraData.NamedKeys.Count, Is.EqualTo(1));
            Assert.That(JsonConvert.DeserializeObject<StatisticsDeviceData>(jsons[statsFeatureRefId]),
                        Is.EqualTo(JsonConvert.DeserializeObject<StatisticsDeviceData>(plugExtraData["data"])));
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
                { "data" , TestHelper.CreateJsonForPastDuationDevice(StatisticsFunction.MaximumValue, trackedDeviceRefId, durationInterval, refreshInterval)}
            };

            // edit
            string data2 = plugIn.Object.PostBackProc("deviceedit", editRequest.ToString(), string.Empty, 0);

            // error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);
            Assert.That(result2, Is.Not.Null);
            StringAssert.Contains((string)result2["error"], $"Device or feature {trackedDeviceRefId} not a plugin feature");
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
                { "data", TestHelper.CreateJsonForPastDuationDevice(function, trackedRefId, durationInterval, refreshInterval) },
            };
            return request;
        }
    }
}