using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Logging;
using Hspi;
using Hspi.Device;
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

        public static long ToUnixTimeMilliseconds(this DateTime time)
        {
            return ((DateTimeOffset)time).ToUnixTimeMilliseconds();
        }

        public static long ToUnixTimeSeconds(this DateTime time)
        {
            return ((DateTimeOffset)time).ToUnixTimeSeconds();
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

        public static (Mock<PlugIn> mockPlugin, FakeHSController mockHsController)
                         CreateMockPluginAndHsController2(Dictionary<string, string> settingsFromIni)
        {
            var mockPlugin = new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };

            var mockHsController = SetupHsControllerAndSettings2(mockPlugin, settingsFromIni);

            mockPlugin.Object.InitIO();

            return (mockPlugin, mockHsController);
        }

        public static void CreateMockPlugInAndHsController2(out Mock<PlugIn> plugin,
                                                            out FakeHSController mockHsController)
        {
            plugin = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);
        }

        public static void CreateMockPlugInAndMoqHsController(out Mock<PlugIn> plugin,
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

        public static void RaiseHSEvent(Mock<PlugIn> plugin, Constants.HSEvent eventType, int refId)
        {
            if (eventType == Constants.HSEvent.VALUE_CHANGE)
            {
                plugin.Object.HsEvent(Constants.HSEvent.VALUE_CHANGE, new object[] { null, null, null, null, refId });
            }
            else
            {
                plugin.Object.HsEvent(Constants.HSEvent.STRING_CHANGE, new object[] { null, null, null, refId });
            }
        }

        public static RecordData RaiseHSEventAndWait(Mock<PlugIn> plugin,
                                                    FakeHSController mockHsController,
                                                    Constants.HSEvent eventType,
                                                    int refId,
                                                    double value,
                                                    string status,
                                                    DateTime lastChange,
                                                    int expectedCount)
        {
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.Value, value);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.DisplayedStatus, status);
            mockHsController.SetupDevOrFeatureValue(refId, EProperty.LastChange, lastChange);

            RaiseHSEvent(plugin, eventType, refId);
            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, refId, expectedCount));
            return new RecordData(refId, value, status, ((DateTimeOffset)lastChange).ToUnixTimeSeconds());
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

        public static FakeHSController SetupHsControllerAndSettings2(Mock<PlugIn> mockPlugin,
                                                                     Dictionary<string, string> settingsFromIni = null)
        {
            var fakeHsController = new FakeHSController();
            if (settingsFromIni != null)
            {
                fakeHsController.SetupIniSettingsSection("Settings", settingsFromIni);
            }

            UpdatePluginHsGet(mockPlugin, fakeHsController);
            return fakeHsController;
        }

        public static DateTime SetUpMockSystemClockForCurrentTime(Mock<PlugIn> plugin)
        {
            var mockClock = new Mock<ISystemClock>(MockBehavior.Strict);
            plugin.Protected().Setup<ISystemClock>("CreateClock").Returns(mockClock.Object);
            DateTime nowTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.Now).Returns(nowTime);
            return nowTime;
        }

        public static void SetupStatisticsDevice(StatisticsFunction statisticsFunction,
                                          Mock<PlugIn> plugIn,
                                          FakeHSController hsControllerMock,
                                          DateTime aTime,
                                          int statsDeviceRefId,
                                          int trackedFeatureRefId)
        {
            Mock<ISystemClock> mockClock = TestHelper.CreateMockSystemClock(plugIn);
            mockClock.Setup(x => x.Now).Returns(aTime.AddSeconds(-1));

            hsControllerMock.SetupFeature(statsDeviceRefId, 12.132, featureInterface: PlugInData.PlugInId);
            hsControllerMock.SetupFeature(trackedFeatureRefId, 2);

            PlugExtraData plugExtraData = new();
            plugExtraData.AddNamed("data", $"{{\"TrackedRef\":{trackedFeatureRefId},\"StatisticsFunction\":{(int)statisticsFunction},\"FunctionDurationSeconds\":600,\"RefreshIntervalSeconds\":30}}");
            hsControllerMock.SetupDevOrFeatureValue(statsDeviceRefId, EProperty.PlugExtraData, plugExtraData);
        }

        public static bool TimedWaitTillTrue(Func<bool> func, TimeSpan wait)
        {
            return SpinWait.SpinUntil(func, wait);
        }

        public static bool TimedWaitTillTrue(Func<bool> func)
        {
            return TimedWaitTillTrue(func, TimeSpan.FromSeconds(30));
        }

        public static void UpdatePluginHsGet(Mock<PlugIn> mockPlugin, FakeHSController fakeHsController)
        {
            // set mock homeseer via reflection
            Type plugInType = typeof(AbstractPlugin);
            var method = plugInType.GetMethod("set_HomeSeerSystem", BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);
            method.Invoke(mockPlugin.Object, new object[] { fakeHsController as IHsController });
        }

        public static HtmlAgilityPack.HtmlDocument VerifyHtmlValid(string html)
        {
            HtmlAgilityPack.HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(html);
            Assert.AreEqual(0, htmlDocument.ParseErrors.Count());
            return htmlDocument;
        }

        public static void WaitForRecordCountAndDeleteAll(Mock<PlugIn> plugIn, int trackedDeviceRefId, int count)
        {
            TestHelper.WaitTillTotalRecords(plugIn, trackedDeviceRefId, count);
            Assert.AreEqual(count, plugIn.Object.DeleteAllRecords(trackedDeviceRefId));
        }

        public static void WaitTillExpectedValue(FakeHSController hsControllerMock,
                                                  int statsDeviceRefId, double expectedValue)
        {
            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                var value = hsControllerMock.GetFeatureValue(statsDeviceRefId, EProperty.Value);
                if (value is double doubleValue)
                {
                    return doubleValue == expectedValue;
                }

                return false;
            }));
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