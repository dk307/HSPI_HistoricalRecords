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
        [DataTestMethod]
        [DataRow("averagestep")]
        [DataRow("averagelinear")]
        public void AddDevice(string function)
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

            Assert.IsTrue(plugIn.Object.InitIO());

            JObject pairRequest = new()
            {
                { "trackedref", new JValue(trackedFeature.Ref) },
                { "function", new JValue(function) },
                { "daysDuration", new JValue(1) },
                { "hoursDuration", new JValue(0) },
                { "minutesDuration", new JValue(10) },
                { "secondsDuration", new JValue(0) },
                { "daysRefresh", new JValue(0) },
                { "hoursRefresh", new JValue(0) },
                { "minutesRefresh", new JValue(1) },
                { "secondsRefresh", new JValue(30) }
            };

            //add
            string data2 = plugIn.Object.PostBackProc("devicecreate", pairRequest.ToString(), string.Empty, 0);

            var result2 = JsonConvert.DeserializeObject<JObject>(data2);

            Assert.IsNotNull(result2);
            Assert.IsNull((string)result2["ErrorMessage"]);

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
                case "averagestep":
                    Assert.IsTrue(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Average(Step)"));
                    Assert.AreEqual(StatisticsFunction.AverageStep, data.StatisticsFunction); break;
                case "averagelinear":
                    Assert.IsTrue(((string)newFeatureData.Feature[EProperty.Name]).StartsWith("Average(Linear)"));
                    Assert.AreEqual(StatisticsFunction.AverageLinear, data.StatisticsFunction); break;
            }
            Assert.AreEqual(new TimeSpan(1, 0, 10, 0), data.FunctionDuration);
            Assert.AreEqual(new TimeSpan(0, 0, 1, 30), data.RefreshInterval);

            CollectionAssert.AreEqual(trackedFeature.StatusGraphics.Values,
                                     ((StatusGraphicCollection)newFeatureData.Feature[EProperty.StatusGraphics]).Values);
            plugIn.Object.ShutdownIO();
        }

        [DataTestMethod]
        [DataRow("{\"trackedref\":1000,\"function\":\"average\",\"daysDuration\":1,\"hoursDuration\":0,\"minutesDuration\":10,\"secondsDuration\":0,\"daysRefresh\":0,\"hoursRefresh\":0,\"minutesRefresh\":1,\"secondsRefresh\":30}", "function is not correct")]
        [DataRow("{\"trackedref\":1000,\"function\":\"averagelinear\",\"hoursDuration\":0,\"minutesDuration\":10,\"secondsDuration\":0,\"daysRefresh\":0,\"hoursRefresh\":0,\"minutesRefresh\":1,\"secondsRefresh\":30}", "daysDuration is not correct")]
        [DataRow("", "trackedref is not correct")]
        public void AddDeviceErrorChecking(string format, string exception)
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            Assert.IsTrue(plugIn.Object.InitIO());

            //add
            string data = plugIn.Object.PostBackProc("devicecreate", format, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, exception);

            plugIn.Object.ShutdownIO();
            plugIn.Object.Dispose();
        }

        [DataTestMethod]
        [DataRow(StatisticsFunction.AverageStep)]
        [DataRow(StatisticsFunction.AverageLinear)]
        public async Task DeviceIsUpdated(StatisticsFunction statisticsFunction)
        {
            CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.CancelAfter(5 * 1000);
            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            DateTime aTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);

            Mock<ISystemClock> mockClock = TestHelper.CreateMockSystemClock(plugIn);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(-1));

            int deviceRefId = 1000;
            hsControllerMock.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, false))
                            .Returns(new List<int>() { deviceRefId });
            HsFeature statsFeature = TestHelper.SetupHsFeature(hsControllerMock, deviceRefId, 12.132, featureInterface: PlugInData.PlugInId);
            HsFeature trackedFeature = TestHelper.SetupHsFeature(hsControllerMock, 19384, 12.132);

            PlugExtraData plugExtraData = new();
            plugExtraData.AddNamed("data", $"{{\"TrackedRef\":{trackedFeature.Ref},\"StatisticsFunction\":{(int)statisticsFunction},\"FunctionDuration\":\"0.00:10:00\",\"RefreshInterval\":\"00:01:30\"}}");

            hsControllerMock.Setup(x => x.GetPropertyByRef(deviceRefId, EProperty.PlugExtraData)).Returns(plugExtraData);
            AsyncManualResetEvent updated = new();

            SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData = new();
            TestHelper.SetupEPropertySet(hsControllerMock, deviceOrFeatureData, (refId, type, value) =>
            {
                if (type == EProperty.Value)
                {
                    updated.Set();
                }
            });

            Assert.IsTrue(plugIn.Object.InitIO());

            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedFeature, 10, "10", aTime.AddMinutes(-10), 1);
            TestHelper.RaiseHSEventAndWait(plugIn, hsControllerMock,
                                           Constants.HSEvent.VALUE_CHANGE,
                                           trackedFeature, 20, "20", aTime.AddMinutes(-5), 2);

            plugIn.Object.UpdateStatisticsFeature(deviceRefId);

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

            plugIn.Object.ShutdownIO();
        }
    }
}