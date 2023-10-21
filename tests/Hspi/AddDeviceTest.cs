﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class AddDeviceTest
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

            Assert.IsTrue(((string)newFeatureData.Feature[EProperty.Name]).StartsWith(trackedFeature.Name));
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

            Assert.AreEqual(4, plugExtraData.NamedKeys.Count);
            Assert.AreEqual(trackedFeature.Ref.ToString(), plugExtraData["trackedRef"]);

            switch (function)
            {
                case "averagestep":
                    Assert.AreEqual(StatisticsFunction.AverageStep.ToString(), plugExtraData["function"]); break;
                case "averagelinear":
                    Assert.AreEqual(StatisticsFunction.AverageLinear.ToString(), plugExtraData["function"]); break;
            }
            Assert.AreEqual(new TimeSpan(1, 0, 10, 0).ToString("c"), plugExtraData["durationInterval"]);
            Assert.AreEqual(new TimeSpan(0, 0, 1, 30).ToString("c"), plugExtraData["refreshInterval"]);

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
    }
}