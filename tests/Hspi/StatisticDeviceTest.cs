using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class StatisticsDeviceTest
    {
        public StatisticsDeviceTest()
        {
            cancellationTokenSource.CancelAfter(30 * 1000);
        }

        [DataTestMethod]
        [DataRow(StatisticsFunction.AverageStep)]
        [DataRow(StatisticsFunction.AverageLinear)]
        public void AddDevice(StatisticsFunction function)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            HsFeature trackedFeature = TestHelper.SetupHsFeature(hsControllerMock, 1000, 1.132);
            trackedFeature.Changes.Add(EProperty.Name, "A Unique Device");
            trackedFeature.Changes.Add(EProperty.Location, "1 Loc");
            trackedFeature.Changes.Add(EProperty.Location2, "2 Loc");
            trackedFeature.Changes.Add(EProperty.AdditionalStatusData, new List<string> { "ad" });

            var collection = new StatusGraphicCollection();
            collection.Add(new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }));
            trackedFeature.Changes.Add(EProperty.StatusGraphics, collection);

            // Capture create device data
            NewDeviceData newDataForDevice = null;
            hsControllerMock.Setup(x => x.CreateDevice(It.IsAny<NewDeviceData>()))
                            .Callback<NewDeviceData>(r => newDataForDevice = r)
                            .Returns(1999);

            NewFeatureData newFeatureData = null;
            hsControllerMock.Setup(x => x.CreateFeatureForDevice(It.IsAny<NewFeatureData>()))
                            .Callback<NewFeatureData>(r => newFeatureData = r)
                            .Returns(2000);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            JObject request = new()
            {
                { "TrackedRef", new JValue(trackedFeature.Ref) },
                { "StatisticsFunction", new JValue(function) },
                { "FunctionDurationSeconds", new JValue((long)new TimeSpan(1, 0, 10, 0).TotalSeconds) },
                { "RefreshIntervalSeconds", new JValue((long)new TimeSpan(0, 0, 1, 30).TotalSeconds) },
            };

            //add
            string data2 = plugIn.Object.PostBackProc("devicecreate", request.ToString(), string.Empty, 0);

            // check no error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);
            Assert.IsNotNull(result2);
            Assert.IsNull((string)result2["error"]);

            // check proper device & feature was added
            Assert.IsNotNull(newDataForDevice);

            Assert.IsTrue(((string)newDataForDevice.Device[EProperty.Name]).StartsWith(trackedFeature.Name));
            Assert.AreEqual(PlugInData.PlugInId, newDataForDevice.Device[EProperty.Interface]);
            Assert.AreEqual(trackedFeature.Location, newDataForDevice.Device[EProperty.Location]);
            Assert.AreEqual(trackedFeature.Location2, newDataForDevice.Device[EProperty.Location2]);

            Assert.AreEqual(PlugInData.PlugInId, newFeatureData.Feature[EProperty.Interface]);
            CollectionAssert.AreEqual(trackedFeature.AdditionalStatusData, (List<string>)newFeatureData.Feature[EProperty.AdditionalStatusData]);
            Assert.AreEqual(trackedFeature.Location, newFeatureData.Feature[EProperty.Location]);
            Assert.AreEqual(trackedFeature.Location2, newFeatureData.Feature[EProperty.Location2]);
            Assert.AreEqual((int)EFeatureDisplayType.Important, newFeatureData.Feature[EProperty.FeatureDisplayType]);
#pragma warning disable S3265 // Non-flags enums should not be used in bitwise operations
            Assert.AreEqual((uint)(EMiscFlag.StatusOnly | EMiscFlag.SetDoesNotChangeLastChange | EMiscFlag.ShowValues),
                            newFeatureData.Feature[EProperty.Misc]);
#pragma warning restore S3265 // Non-flags enums should not be used in bitwise operations
            CollectionAssert.AreEqual((new HashSet<int> { 1999 }).ToImmutableArray(),
                                     ((HashSet<int>)newFeatureData.Feature[EProperty.AssociatedDevices]).ToImmutableArray());

            var plugExtraData = (PlugExtraData)newFeatureData.Feature[EProperty.PlugExtraData];

            Assert.AreEqual(1, plugExtraData.NamedKeys.Count);

            var data = JsonConvert.DeserializeObject<StatisticsDeviceData>(plugExtraData["data"]);

            Assert.AreEqual(trackedFeature.Ref, data.TrackedRef);

            switch (function)
            {
                case StatisticsFunction.AverageStep:
                    Assert.IsTrue(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Average(Step)"));
                    break;

                case StatisticsFunction.AverageLinear:
                    Assert.IsTrue(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Average(Linear)"));
                    break;
            }

            Assert.AreEqual(function, data.StatisticsFunction);

            Assert.AreEqual((long)new TimeSpan(1, 0, 10, 0).TotalSeconds, data.FunctionDurationSeconds);

            Assert.AreEqual((long)new TimeSpan(0, 0, 1, 30).TotalSeconds, data.RefreshIntervalSeconds);

            CollectionAssert.AreEqual(trackedFeature.StatusGraphics.Values,
                                     ((StatusGraphicCollection)newFeatureData.Feature[EProperty.StatusGraphics]).Values);
        }

        [DataTestMethod]
        [DataRow(StatisticsFunction.AverageStep)]
        [DataRow(StatisticsFunction.AverageLinear)]
        public void EditDevice(StatisticsFunction function)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 1000;
            AsyncManualResetEvent updated = new();

            SetupStatisticsDevice(StatisticsFunction.AverageLinear, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, updated, out var statsFeature, out var trackedFeature,
                                  out var deviceOrFeatureData);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            JObject editRequest = new()
            {
                { "ref" , new JValue(statsDeviceRefId) },
                { "data" , new JObject() {
                    { "TrackedRef", new JValue(trackedFeature.Ref) },
                    { "StatisticsFunction", new JValue(function) },
                    { "FunctionDurationSeconds", new JValue((long)new TimeSpan(5, 1, 10, 3).TotalSeconds) },
                    { "RefreshIntervalSeconds", new JValue((long)new TimeSpan(0, 5, 1, 30).TotalSeconds) },
                }}
            };

            // edit
            string data2 = plugIn.Object.PostBackProc("deviceedit", editRequest.ToString(), string.Empty, 0);

            // no error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);
            Assert.IsNotNull(result2);
            Assert.IsNull((string)result2["error"]);

            hsControllerMock.Setup(x => x.GetPropertyByRef(statsDeviceRefId, EProperty.PlugExtraData))
                            .Returns(deviceOrFeatureData[statsDeviceRefId][EProperty.PlugExtraData]);

            // get return function value for feature
            string json = plugIn.Object.GetStatisticDeviceDataAsJson(statsDeviceRefId);
            Assert.AreEqual(JsonConvert.DeserializeObject<StatisticsDeviceData>(editRequest["data"].ToString()),
                            JsonConvert.DeserializeObject<StatisticsDeviceData>(json));

            // check value was set in HS4 was set
            var plugExtraData = (PlugExtraData)deviceOrFeatureData[statsDeviceRefId][EProperty.PlugExtraData];
            Assert.AreEqual(1, plugExtraData.NamedKeys.Count);
            Assert.AreEqual(JsonConvert.DeserializeObject<StatisticsDeviceData>(plugExtraData["data"]),
                            JsonConvert.DeserializeObject<StatisticsDeviceData>(json));
        }

        [TestMethod]
        public void EditDeviceFailsForInvalidDevice()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 1000;
            AsyncManualResetEvent updated = new();

            SetupStatisticsDevice(StatisticsFunction.AverageLinear, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, updated, out var _, out var trackedFeature,
                                  out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            JObject editRequest = new()
            {
                { "ref" , new JValue(trackedFeature.Ref) }, // wrong ref
                { "data" , new JObject() {
                    { "TrackedRef", new JValue(trackedFeature.Ref) },
                    { "StatisticsFunction", new JValue(StatisticsFunction.AverageLinear) },
                    { "FunctionDurationSeconds", new JValue((long)new TimeSpan(5, 1, 10, 3).TotalSeconds) },
                    { "RefreshIntervalSeconds", new JValue((long)new TimeSpan(0, 5, 1, 30).TotalSeconds) },
                }}
            };

            // edit
            string data2 = plugIn.Object.PostBackProc("deviceedit", editRequest.ToString(), string.Empty, 0);

            // error is returned
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);
            Assert.IsNotNull(result2);
            StringAssert.Contains((string)result2["error"], $"Device/Feature {trackedFeature.Ref} not a plugin feature");
        }

        [DataTestMethod]
        [DataRow("{\"StatisticsFunction\":3,\"FunctionDurationSeconds\":0,\"RefreshIntervalSeconds\":10}", "Required property 'TrackedRef' not found in JSON")]
        [DataRow("", "data is not correct")]
        public void AddDeviceErrorChecking(string format, string exception)
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            //add
            string data = plugIn.Object.PostBackProc("devicecreate", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, exception);
        }

        [DataTestMethod]
        [DataRow(StatisticsFunction.AverageStep)]
        [DataRow(StatisticsFunction.AverageLinear)]
        public async Task DeviceIsUpdated(StatisticsFunction statisticsFunction)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 1000;
            AsyncManualResetEvent updated = new();

            SetupStatisticsDevice(statisticsFunction, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, updated, out var statsFeature, out var trackedFeature,
                                  out var deviceOrFeatureData);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedFeature, 10, "10", aTime.AddMinutes(-10), 1);
            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedFeature, 20, "20", aTime.AddMinutes(-5), 2);

            plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId);

            await updated.WaitAsync(cancellationTokenSource.Token);

            Assert.AreEqual(false, deviceOrFeatureData[statsFeature.Ref][EProperty.InvalidValue]);

            double ExpectedValue = 0;
            switch (statisticsFunction)
            {
                case StatisticsFunction.AverageStep:
                    ExpectedValue = ((10D * 5 * 60) + (20D * 5 * 60)) / 600D; break;
                case StatisticsFunction.AverageLinear:
                    ExpectedValue = ((15D * 5 * 60) + (20D * 5 * 60)) / 600D; break;
                default:
                    Assert.Fail();
                    break;
            }

            Assert.AreEqual(ExpectedValue, deviceOrFeatureData[statsFeature.Ref][EProperty.Value]);
        }

        [TestMethod]
        public async Task DeviceIsUpdatedRounded()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 1000;
            AsyncManualResetEvent updated = new();

            SetupStatisticsDevice(StatisticsFunction.AverageStep, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, updated, out var statsFeature, out var trackedFeature,
                                  out var deviceOrFeatureData);

            List<StatusGraphic> statusGraphics = new() { new StatusGraphic("path", new ValueRange(int.MinValue, int.MaxValue) { DecimalPlaces = 1 }) };
            hsControllerMock.Setup(x => x.GetPropertyByRef(trackedFeature.Ref, EProperty.StatusGraphics)).Returns(statusGraphics);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedFeature, 11.85733, "11.2", aTime.AddMinutes(-10), 1);

            plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId);

            await updated.WaitAsync(cancellationTokenSource.Token);

            Assert.AreEqual(false, deviceOrFeatureData[statsFeature.Ref][EProperty.InvalidValue]);

            Assert.AreEqual(11.9D, deviceOrFeatureData[statsFeature.Ref][EProperty.Value]);
        }

        [TestMethod]
        public void StatisticsDeviceIsDeleted()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            int statsDeviceRefId = 1000;
            AsyncManualResetEvent updated = new();

            SetupStatisticsDevice(StatisticsFunction.AverageStep, plugIn, hsControllerMock, aTime,
                                  statsDeviceRefId, updated, out var statsFeature, out var trackedFeature,
                                  out var _);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            Assert.IsTrue(plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId));

            hsControllerMock.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, It.IsAny<bool>())).Returns(new List<int>());
            plugIn.Object.HsEvent(Constants.HSEvent.CONFIG_CHANGE,
                                  new object[] { null, null, null, statsDeviceRefId, 2 });

            TestHelper.TimedWaitTillTrue(() => !plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId));

            // not more tracking after delete
            Assert.IsFalse(plugIn.Object.UpdateStatisticsFeature(statsDeviceRefId));
        }

        private static void SetupStatisticsDevice(StatisticsFunction statisticsFunction, Mock<PlugIn> plugIn,
                                                  Mock<IHsController> hsControllerMock,
                                                  DateTime aTime,
                                                  int deviceRefId,
                                                  AsyncManualResetEvent updated,
                                                  out HsFeature statsFeature,
                                                  out HsFeature trackedFeature,
                                                  out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData)
        {
            Mock<ISystemClock> mockClock = TestHelper.CreateMockSystemClock(plugIn);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(-1));

            hsControllerMock.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, false))
                            .Returns(new List<int>() { deviceRefId });
            statsFeature = TestHelper.SetupHsFeature(hsControllerMock, deviceRefId, 12.132, featureInterface: PlugInData.PlugInId);
            trackedFeature = TestHelper.SetupHsFeature(hsControllerMock, 19384, 12.132);
            PlugExtraData plugExtraData = new();
            plugExtraData.AddNamed("data", $"{{\"TrackedRef\":{trackedFeature.Ref},\"StatisticsFunction\":{(int)statisticsFunction},\"FunctionDurationSeconds\":600,\"RefreshIntervalSeconds\":30}}");

            hsControllerMock.Setup(x => x.GetPropertyByRef(deviceRefId, EProperty.PlugExtraData)).Returns(plugExtraData);
            deviceOrFeatureData = new();
            TestHelper.SetupEPropertySet(hsControllerMock, deviceOrFeatureData, (refId, type, value) =>
            {
                if (type == EProperty.Value)
                {
                    updated.Set();
                }
            });
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}