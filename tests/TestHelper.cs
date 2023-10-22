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
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public static void CreateMockPlugInAndHsController(out Mock<PlugIn> plugin,
                                                           out Mock<IHsController> mockHsController)
        {
            plugin = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());
        }

        public static Mock<ISystemClock> CreateMockSystemClock(Mock<PlugIn> plugIn)
        {
            var mockClock = new Mock<ISystemClock>(MockBehavior.Strict);
            plugIn.Protected().Setup<ISystemClock>("CreateClock").Returns(mockClock.Object);
            return mockClock;
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

        public static void SetupEPropertySet(Mock<IHsController> mockHsController,
                                             SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData,
                                             Action<int, EProperty, object> updateValueCallback = null)
        {
            mockHsController.Setup(x => x.UpdateFeatureValueByRef(It.IsAny<int>(), It.IsAny<double>()))
                .Returns((int devOrFeatRef, double value) =>
                {
                    AddValue(devOrFeatRef, EProperty.Value, value);
                    updateValueCallback?.Invoke(devOrFeatRef, EProperty.Value, value);
                    return true;
                });

            mockHsController.Setup(x => x.UpdateFeatureValueStringByRef(It.IsAny<int>(), It.IsAny<string>()))
                .Returns((int devOrFeatRef, string value) =>
                {
                    AddValue(devOrFeatRef, EProperty.StatusString, value);
                    updateValueCallback?.Invoke(devOrFeatRef, EProperty.StatusString, value);
                    return true;
                });

            mockHsController.Setup(x => x.UpdatePropertyByRef(It.IsAny<int>(), It.IsAny<EProperty>(), It.IsAny<object>()))
                .Callback((int devOrFeatRef, EProperty property, object value) =>
                {
                    AddValue(devOrFeatRef, property, value);
                    updateValueCallback?.Invoke(devOrFeatRef, property, value);
                });

            void AddValue(int devOrFeatRef, EProperty property, object value)
            {
                if (deviceOrFeatureData.TryGetValue(devOrFeatRef, out var dict))
                {
                    dict[property] = value;
                }
                else
                {
                    var dict2 = new Dictionary<EProperty, object>
                    {
                        { property, value }
                    };
                    deviceOrFeatureData[devOrFeatRef] = dict2;
                }
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
            mockHsController.Setup(x => x.GetAllRefs()).Returns(new List<int>());
            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, It.IsAny<bool>())).Returns(new List<int>());
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

            mockHsController.Setup(x => x.GetPropertyByRef(deviceRefId, EProperty.Interface)).Returns("Z-Wave");
            mockHsController.Setup(x => x.GetPropertyByRef(deviceRefId, EProperty.DeviceType)).Returns(new HomeSeer.PluginSdk.Devices.Identification.TypeInfo() { ApiType = EApiType.Feature });
            mockHsController.Setup(x => x.GetPropertyByRef(deviceRefId, EProperty.Relationship)).Returns(ERelationship.Feature);
            mockHsController.Setup(x => x.GetPropertyByRef(deviceRefId, EProperty.StatusGraphics)).Returns(new List<StatusGraphic>());
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

        public static HtmlAgilityPack.HtmlDocument VerifyHtmlValid(string html)
        {
            HtmlAgilityPack.HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(html);
            Assert.AreEqual(0, htmlDocument.ParseErrors.Count());
            return htmlDocument;
        }

        public static bool WaitTillTotalRecords(Mock<PlugIn> plugin, int refId, long count)
        {
            return (TimedWaitTillTrue(() =>
            {
                return plugin.Object.GetTotalRecords(refId) == count;
            }));
        }
    }

    internal sealed class PlugInLifeCycle : IDisposable
    {
        public PlugInLifeCycle(Mock<Hspi.PlugIn> plugIn)
        {
            this.plugIn = plugIn;
            Assert.IsTrue(plugIn.Object.InitIO());
        }

        void IDisposable.Dispose()
        {
            plugIn.Object.ShutdownIO();
            plugIn.Object.Dispose();
        }

        private readonly Mock<Hspi.PlugIn> plugIn;
    }
}