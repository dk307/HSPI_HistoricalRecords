using System;
using System.IO;
using HomeSeer.PluginSdk;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static HomeSeer.PluginSdk.PluginStatus;

namespace HSPI_HistoricalRecordsTest

{
    [TestClass]
    public class BackupTest
    {
        [TestMethod]
        public void BackupDatabaseDoesNotLooseChanges()
        {
            SetupPlugin(out var plugin, out var mockHsController);
            int deviceRefId = 1000;
            mockHsController.SetupFeature(deviceRefId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, deviceRefId, 1);

            FireBackupStartEvent(plugin);

            TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.VALUE_CHANGE, deviceRefId);

            FireBackupStopEvent(plugin);

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1));
        }

        [TestMethod]
        public void BackupEndMultipleTimes()
        {
            SetupPlugin(out var plugin, out var mockHsController);

            int deviceRefId = 1000;
            mockHsController.SetupFeature(deviceRefId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1));

            FireBackupStartEvent(plugin);

            FireBackupStopEvent(plugin);
            FireBackupStopEvent(plugin);

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            Assert.AreEqual(1, plugin.Object.GetTotalRecords(deviceRefId));
        }

        [TestMethod]
        public void BackupStopsDatabase()
        {
            SetupPlugin(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            FireBackupStartEvent(plugin);

            // Verify the db operations fail
            Assert.ThrowsException<InvalidOperationException>(() => plugin.Object.PruneDatabase());

            FireBackupStopEvent(plugin);

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            plugin.Object.PruneDatabase();
        }

        [TestMethod]
        public void BackupTimesOut()
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings2(plugin);
            var mockClock = TestHelper.CreateMockSystemGlobalTimerAndClock(plugin);
            mockClock.Setup(x => x.IntervalToRetrySqliteCollection).Returns(TimeSpan.FromMilliseconds(5));
            mockClock.Setup(x => x.TimeoutForBackup).Returns(TimeSpan.FromMilliseconds(5));

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            FireBackupStartEvent(plugin);

            // Verify the db operations fail
            Assert.ThrowsException<InvalidOperationException>(() => plugin.Object.PruneDatabase());

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            plugin.Object.PruneDatabase();
        }

        [TestMethod]
        public void CanAccessDBFileAfterBackupStarts()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var hsMockController = TestHelper.SetupHsControllerAndSettings2(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            FireBackupStartEvent(plugin);

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                try
                {
                    File.Delete(hsMockController.DBPath);
                }
                catch
                {
                    // ignore all
                }

                return !File.Exists(hsMockController.DBPath);
            }));
        }

        [TestMethod]
        public void PluginStatusDuringBackup()
        {
            SetupPlugin(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            FireBackupStartEvent(plugin);

            var statusBackup = plugin.Object.OnStatusCheck();
            Assert.AreEqual(EPluginStatus.Warning, statusBackup.Status);
            Assert.AreEqual("Device records are not being stored", statusBackup.StatusText);

            FireBackupStopEvent(plugin);

            Assert.IsTrue(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            var status = plugin.Object.OnStatusCheck();
            Assert.AreEqual(EPluginStatus.Ok, status.Status);
        }

        private static void FireBackupStartEvent(Mock<PlugIn> plugin)
        {
            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 1 });
        }

        private static void FireBackupStopEvent(Mock<PlugIn> plugin)
        {
            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 2 });
        }

        private static void SetupPlugin(out Mock<PlugIn> plugin, out FakeHSController mockHsController)
        {
            plugin = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);
            var mockClock = TestHelper.CreateMockSystemGlobalTimerAndClock(plugin);
            mockClock.Setup(x => x.IntervalToRetrySqliteCollection).Returns(TimeSpan.FromMilliseconds(5));
        }

        private const Constants.HSEvent BackUpEvent = (Constants.HSEvent)0x200;
    }
}