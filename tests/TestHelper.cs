using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Logging;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HSPI_HistoricalRecordsTest
{
    internal class TestHelper
    {
        public static bool TimedWaitTillTrue(Func<bool> func, TimeSpan wait)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            bool result = func();
            while (!result && stopwatch.Elapsed < wait)
            {
                Thread.Sleep(100);
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

        public static Mock<PlugIn> CreatePlugInMock()
        {
            return new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };
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
            mockHsController.Setup(x => x.GetINISetting("Settings", "gGlobalTempScaleF", "True", "")).Returns("True");
            mockHsController.Setup(x => x.GetIniSection("Settings", PlugInData.PlugInId + ".ini")).Returns(settingsFromIni);
            mockHsController.Setup(x => x.SaveINISetting("Settings", It.IsAny<string>(), It.IsAny<string>(), PlugInData.PlugInId + ".ini"));
            mockHsController.Setup(x => x.WriteLog(It.IsAny<ELogType>(), It.IsAny<string>(), PlugInData.PlugInName, It.IsAny<string>()));
            mockHsController.Setup(x => x.RegisterDeviceIncPage(PlugInData.PlugInId, It.IsAny<string>(), It.IsAny<string>()));
            mockHsController.Setup(x => x.RegisterFeaturePage(PlugInData.PlugInId, It.IsAny<string>(), It.IsAny<string>()));
            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, true)).Returns(new List<int>());
            mockHsController.Setup(x => x.GetNameByRef(It.IsAny<int>())).Returns("Test");
            mockHsController.Setup(x => x.RegisterEventCB(It.IsAny<Constants.HSEvent>(), PlugInData.PlugInId));
            return mockHsController;
        }
    }
}