using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Logging;
using Hspi;
using Hspi.Device;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    internal static class Extensions
    {
        public static List<RecordData> Clone(this List<RecordData> listToClone)
        {
            return listToClone.Select(item => item with { }).ToList();
        }

        public static List<RecordDataAndDuration> Clone(this List<RecordDataAndDuration> listToClone)
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
            Assert.That(TimedWaitTillTrue(() =>
            {
                var records = GetHistoryRecords(plugin, refId, askForRecordCount);
                Assert.That(records, Is.Not.Null);
                if (records.Count == 0)
                {
                    return false;
                }

                Assert.That(expectedRecordCount, Is.EqualTo(records.Count));
                Assert.That(records.Count >= 1);
                Assert.That(records[0].DeviceRefId, Is.EqualTo(recordData.DeviceRefId));
                Assert.That(records[0].DeviceValue, Is.EqualTo(recordData.DeviceValue));
                Assert.That(records[0].DeviceString, Is.EqualTo(recordData.DeviceString));
                Assert.That(records[0].UnixTimeSeconds, Is.EqualTo(recordData.UnixTimeSeconds));
                return true;
            }));
        }

        public static void CheckRecordedValueForFeatureType(Mock<PlugIn> plugin, HsFeature feature,
                                                       int askForRecordCount, int expectedRecordCount)
        {
            RecordData recordData = new(feature.Ref, feature.Value, feature.DisplayedStatus, ((DateTimeOffset)feature.LastChange).ToUnixTimeSeconds());
            CheckRecordedValue(plugin, feature.Ref, recordData, askForRecordCount, expectedRecordCount);
        }

        public static JObject CreateJsonForDevice(StatisticsFunction function,
                                                  int trackedRefId,
                                                  long durationInterval,
                                                  long refreshInterval)
        {
            var end = new JObject() {
                { "Type" , new JValue("Now")  }
            };

            var period = new JObject() {
                { "End", end },
                { "FunctionDurationSeconds", new JValue(durationInterval) },
            };

            var duration = new JObject() {
                { "CustomPeriod", period },
            };

            JObject data = new()
            {
                { "TrackedRef", new JValue(trackedRefId) },
                { "StatisticsFunction", new JValue(function) },
                { "StatisticsFunctionDuration", duration },
                { "RefreshIntervalSeconds", new JValue(refreshInterval) }
            };

            return data;
        }

        public static JObject CreateJsonForDevice(StatisticsFunction function,
                                                  int trackedRefId,
                                                  PreDefinedPeriod preDefinedPeriod,
                                                  long refreshInterval)
        {
            var duration = new JObject() {
                { "PreDefinedPeriod", new JValue(preDefinedPeriod) },
            };

            JObject data = new()
            {
                { "TrackedRef", new JValue(trackedRefId) },
                { "StatisticsFunction", new JValue(function) },
                { "StatisticsFunctionDuration", duration },
                { "RefreshIntervalSeconds", new JValue(refreshInterval) }
            };

            return data;
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

        public static void CreateMockPlugInAndHsController2(out Mock<PlugIn> plugin,
                                                            out FakeHSController mockHsController)
        {
            plugin = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);
        }

        public static void CreateMockPlugInAndHsController2(Dictionary<string, string> settingsFromIni,
                                                            out Mock<PlugIn> plugin,
                                                            out FakeHSController mockHsController)
        {
            plugin = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin, settingsFromIni);
        }

        public static void CreateMockPlugInAndMoqHsController(out Mock<PlugIn> plugin,
                                                              out Mock<IHsController> mockHsController)
        {
            plugin = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());
        }

        public static Mock<IGlobalTimerAndClock> CreateMockSystemGlobalTimerAndClock(Mock<PlugIn> plugIn)
        {
            var mockClock = new Mock<IGlobalTimerAndClock>(MockBehavior.Strict);
            plugIn.Protected().Setup<IGlobalTimerAndClock>("CreateClock").Returns(mockClock.Object);
            mockClock.Setup(x => x.FirstDayOfWeek).Returns(DayOfWeek.Monday);
            mockClock.Setup(x => x.IntervalToRetrySqliteCollection).Returns(TimeSpan.FromSeconds(600));
            mockClock.Setup(x => x.TimeoutForBackup).Returns(TimeSpan.FromSeconds(600));
            mockClock.Setup(x => x.MaintenanceInterval).Returns(TimeSpan.FromSeconds(600));
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
                Assert.That(jsonData, Is.Not.Null);
                var records = (JArray)jsonData["data"];
                Assert.That(records, Is.Not.Null);

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
            Assert.That(TestHelper.WaitTillTotalRecords(plugin, refId, expectedCount));
            return new RecordData(refId, value, status, lastChange.ToUnixTimeSeconds());
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
            var mockClock = CreateMockSystemGlobalTimerAndClock(plugin);
            DateTime nowTime = new(2222, 2, 2, 2, 2, 2, DateTimeKind.Local);
            mockClock.Setup(x => x.UtcNow).Returns(nowTime);
            return nowTime;
        }

        public static void SetupPerDeviceSettings(FakeHSController mockHsController, int refId,
                                                  bool tracked, double? minValue = null, double? maxValue = null)
        {
            mockHsController.SetupIniValue("Settings", "DeviceSettings", refId.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "RefId", refId.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "IsTracked", tracked.ToString());
            mockHsController.SetupIniValue(refId.ToString(), "RetentionPeriod", string.Empty);
            mockHsController.SetupIniValue(refId.ToString(), "MinValue", minValue?.ToString("g") ?? string.Empty);
            mockHsController.SetupIniValue(refId.ToString(), "MaxValue", maxValue?.ToString("g") ?? string.Empty);
        }

        public static void SetupStatisticsFeature(StatisticsFunction statisticsFunction,
                                                  Mock<PlugIn> plugIn,
                                                  FakeHSController hsControllerMock,
                                                  DateTimeOffset aTime,
                                                  int statsDeviceRefId,
                                                  int statsFeatureRefId,
                                                  int trackedFeatureRefId)
        {
            Mock<IGlobalTimerAndClock> mockClock = TestHelper.CreateMockSystemGlobalTimerAndClock(plugIn);
            mockClock.Setup(x => x.LocalNow).Returns(aTime);

            hsControllerMock.SetupDevice(statsDeviceRefId, deviceInterface: PlugInData.PlugInId);

            hsControllerMock.SetupFeature(statsFeatureRefId, 12.132, featureInterface: PlugInData.PlugInId);
            hsControllerMock.SetupFeature(trackedFeatureRefId, 2);

            hsControllerMock.SetupDevOrFeatureValue(statsFeatureRefId, EProperty.AssociatedDevices, new HashSet<int> { statsDeviceRefId });
            hsControllerMock.SetupDevOrFeatureValue(statsDeviceRefId, EProperty.AssociatedDevices, new HashSet<int> { statsFeatureRefId });

            PlugExtraData plugExtraData = new();
            string json = CreateJsonForDevice(statisticsFunction, trackedFeatureRefId, 600, 60).ToString();
            plugExtraData.AddNamed("data", json);
            hsControllerMock.SetupDevOrFeatureValue(statsFeatureRefId, EProperty.PlugExtraData, plugExtraData);
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
            Assert.That(htmlDocument.ParseErrors.Count(), Is.EqualTo(0));
            return htmlDocument;
        }

        public static void WaitForRecordCountAndDeleteAll(Mock<PlugIn> plugIn, int trackedDeviceRefId, int count)
        {
            TestHelper.WaitTillTotalRecords(plugIn, trackedDeviceRefId, count);
            Assert.That(plugIn.Object.DeleteAllRecords(trackedDeviceRefId), Is.EqualTo(count));
        }

        public static void WaitTillExpectedValue(FakeHSController hsControllerMock,
                                                  int statsDeviceRefId, double expectedValue)
        {
            Assert.That(TestHelper.TimedWaitTillTrue(() =>
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
            Assert.That(plugIn.Object.InitIO());
        }

        void IDisposable.Dispose()
        {
            plugIn.Object.ShutdownIO();
            plugIn.Object.Dispose();
        }

        private readonly Mock<Hspi.PlugIn> plugIn;
    }
}