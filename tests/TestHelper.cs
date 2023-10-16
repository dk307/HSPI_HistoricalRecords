using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using HomeSeer.PluginSdk.Logging;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace HSPI_HistoricalRecordsTest
{
    internal static class Extensions
    {
        public static List<RecordData> Clone(this List<RecordData> listToClone)
        {
            return listToClone.Select(item => item with { }).ToList();
        }
    }

    internal static class TestHelper
    {
        public static void CheckRecordedValue(Mock<PlugIn> plugin, int refId, RecordData recordData,
                                               int askForRecordCount, int expectedRecordCount)
        {
            Assert.IsTrue(TimedWaitTillTrue(() =>
            {
                var records = GetHistoryRecords(plugin, refId, askForRecordCount);
                Assert.IsNotNull(records);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.AreEqual(records.Count, expectedRecordCount);
                Assert.IsTrue(records.Count >= 1);
                Assert.AreEqual(recordData.DeviceRefId, records[0].DeviceRefId);
                Assert.AreEqual(recordData.DeviceValue, records[0].DeviceValue);
                Assert.AreEqual(recordData.DeviceString, records[0].DeviceString);
                Assert.AreEqual(recordData.UnixTimeSeconds, records[0].UnixTimeSeconds);
                return true;
            }));
        }

        public static void CheckRecordedValueForFeatureType(Mock<PlugIn> plugin, HsFeature feature,
                                                       int askForRecordCount, int expectedRecordCount)
        {
            RecordData recordData = new(feature.Ref, feature.Value, feature.DisplayedStatus, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
            CheckRecordedValue(plugin, feature.Ref, recordData, askForRecordCount, expectedRecordCount);
        }

        public static (Mock<PlugIn> mockPlugin, Mock<IHsController> mockHsController)
                         CreateMockPluginAndHsController(Dictionary<string, string> settingsFromIni)
        {
            var mockPlugin = new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };

            var mockHsController = SetupHsControllerAndSettings(mockPlugin, settingsFromIni);

            mockPlugin.Object.InitIO();

            return (mockPlugin, mockHsController);
        }

        public static Mock<PlugIn> CreatePlugInMock()
        {
            return new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };
        }

        public static List<RecordDataAndDuration> GetHistoryRecords(Mock<PlugIn> plugin, int refId, int recordLimit = 10)
        {
            string paramsForRecord = $"refId={refId}&min=0&max={long.MaxValue}&start=0&length={recordLimit}";
            return GetHistoryRecords(plugin, refId, paramsForRecord);
        }

        public static List<RecordDataAndDuration> GetHistoryRecords(Mock<PlugIn> plugin, int refId, string paramsForRecord)
        {
            List<RecordDataAndDuration> result = new();
            string data = plugin.Object.PostBackProc("historyrecords", paramsForRecord, string.Empty, 0);
            if (!string.IsNullOrEmpty(data))
            {
                var jsonData = (JObject)JsonConvert.DeserializeObject(data);
                Assert.IsNotNull(jsonData);
                var records = (JArray)jsonData["data"];
                Assert.IsNotNull(records);

                foreach (var record in records)
                {
                    var recordArray = (JArray)record;
                    var rd = new RecordDataAndDuration(refId,
                                              recordArray[1].Value<double>(),
                                              recordArray[2].Value<string>(),
                                              recordArray[0].Value<long>() / 1000,
                                              durationSeconds: (long?)recordArray[3]);
                    result.Add(rd);
                }
            }

            return result;
        }

        public static RecordData RaiseHSEventAndWait(Mock<PlugIn> plugin,
                                                     Mock<IHsController> mockHsController,
                                                     Constants.HSEvent eventType,
                                                     HsFeature feature,
                                                     double value,
                                                     string status,
                                                     DateTime lastChange,
                                                     int expectedCount)
        {
            RaiseHSEvent(plugin, mockHsController, eventType, feature, value, status, lastChange);
            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, feature.Ref, expectedCount));
            return new RecordData(feature.Ref, feature.Value, feature.DisplayedStatus, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
        }

        public static void RaiseHSEvent(Mock<PlugIn> plugin, Mock<IHsController> mockHsController, Constants.HSEvent eventType, HsFeature feature, double value, string status, DateTime lastChange)
        {
            feature.Changes[EProperty.Value] = value;
            feature.Changes[EProperty.DisplayedStatus] = status;
            feature.Changes[EProperty.LastChange] = lastChange;

            RaiseHSEvent(plugin, mockHsController, feature, eventType);
        }

        public static void RaiseHSEvent(Mock<PlugIn> plugin, Mock<IHsController> mockHsController, HsFeature feature, Constants.HSEvent eventType)
        {
            foreach (var change in feature.Changes)
            {
                mockHsController.Setup(x => x.GetPropertyByRef(feature.Ref, change.Key)).Returns(change.Value);
            }

            if (eventType == Constants.HSEvent.VALUE_CHANGE)
            {
                plugin.Object.HsEvent(Constants.HSEvent.VALUE_CHANGE, new object[] { null, null, null, null, feature.Ref });
            }
            else
            {
                plugin.Object.HsEvent(Constants.HSEvent.STRING_CHANGE, new object[] { null, null, null, feature.Ref });
            }
        }

        public static Mock<IHsController> SetupHsControllerAndSettings(Mock<PlugIn> mockPlugin,
                                                                       Dictionary<string, string> settingsFromIni)
        {
            var mockHsController = new Mock<IHsController>(MockBehavior.Strict);

            // set mock homeseer via reflection
            Type plugInType = typeof(AbstractPlugin);
            var method = plugInType.GetMethod("set_HomeSeerSystem", BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);
            method.Invoke(mockPlugin.Object, new object[] { mockHsController.Object });

            mockHsController.Setup(x => x.GetAppPath()).Returns(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            mockHsController.Setup(x => x.GetINISetting("Settings", "DeviceSettings", null, PlugInData.SettingFileName)).Returns(string.Empty);
            mockHsController.Setup(x => x.GetIniSection("Settings", PlugInData.SettingFileName)).Returns(settingsFromIni);
            mockHsController.Setup(x => x.SaveINISetting("Settings", It.IsAny<string>(), It.IsAny<string>(), PlugInData.SettingFileName));
            mockHsController.Setup(x => x.WriteLog(It.IsAny<ELogType>(), It.IsAny<string>(), PlugInData.PlugInName, It.IsAny<string>()));
            mockHsController.Setup(x => x.RegisterDeviceIncPage(PlugInData.PlugInId, It.IsAny<string>(), It.IsAny<string>()));
            mockHsController.Setup(x => x.RegisterFeaturePage(PlugInData.PlugInId, It.IsAny<string>(), It.IsAny<string>()));
            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, true)).Returns(new List<int>());
            mockHsController.Setup(x => x.GetNameByRef(It.IsAny<int>())).Returns("Test");
            mockHsController.Setup(x => x.GetAllRefs()).Returns(new List<int>());
            mockHsController.Setup(x => x.RegisterEventCB(It.IsAny<Constants.HSEvent>(), PlugInData.PlugInId));
            return mockHsController;
        }

        public static HsFeature SetupHsFeature(Mock<IHsController> mockHsController, int deviceRefId,
                                                IDictionary<EProperty, object> changes)
        {
            HsFeature feature = new(deviceRefId);
            foreach (var change in changes)
            {
                mockHsController.Setup(x => x.GetPropertyByRef(deviceRefId, change.Key)).Returns(change.Value);
                feature.Changes.Add(change.Key, change.Value);
            }

            mockHsController.Setup(x => x.GetPropertyByRef(deviceRefId, EProperty.PlugExtraData)).Returns(new PlugExtraData());
            mockHsController.Setup(x => x.GetFeatureByRef(deviceRefId)).Returns(feature);
            return feature;
        }

        public static HsFeature SetupHsFeature(Mock<IHsController> mockHsController,
                                    int deviceRefId,
                                    double value,
                                    string displayString = null,
                                    DateTime? lastChange = null,
                                    string featureInterface = null)
        {
            return SetupHsFeature(mockHsController, deviceRefId, new Dictionary<EProperty, object>() {
                    { EProperty.Interface, featureInterface },
                    { EProperty.Value, value },
                    { EProperty.DisplayedStatus, displayString },
                    { EProperty.LastChange, lastChange ?? DateTime.Now },
                });
        }

        public static bool TimedWaitTillTrue(Func<bool> func, TimeSpan wait)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            bool result = func();
            while (!result && stopwatch.Elapsed < wait)
            {
                Thread.Yield();
                result = func();
            }
            return result;
        }

        public static bool TimedWaitTillTrue(Func<bool> func)
        {
            return TimedWaitTillTrue(func, TimeSpan.FromSeconds(30));
        }

        public static void VerifyHtmlValid(string html)
        {
            HtmlAgilityPack.HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(html);
            Assert.AreEqual(0, htmlDocument.ParseErrors.Count());
        }

        public static bool WaitTillTotalRecords(Mock<PlugIn> plugin, int refId, long count)
        {
            return (TimedWaitTillTrue(() =>
            {
                return plugin.Object.GetTotalRecords(refId) == count;
            }));
        }
    }
}