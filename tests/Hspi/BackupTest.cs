using System;
using System.IO;
using HomeSeer.PluginSdk;
using Hspi;
using NUnit.Framework;
using Moq;
using static HomeSeer.PluginSdk.PluginStatus;

namespace HSPI_HistoricalRecordsTest

{
    [TestFixture]
    public class BackupTest
    {
        [Test]
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

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            Assert.That(TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1));
        }

        [Test]
        public void BackupEndMultipleTimes()
        {
            SetupPlugin(out var plugin, out var mockHsController);

            int deviceRefId = 1000;
            mockHsController.SetupFeature(deviceRefId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.That(TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1));

            FireBackupStartEvent(plugin);

            FireBackupStopEvent(plugin);
            FireBackupStopEvent(plugin);

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            Assert.That(plugin.Object.GetTotalRecords(deviceRefId), Is.EqualTo(1));
        }

        [Test]
        public void BackupStopsDatabase()
        {
            SetupPlugin(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            FireBackupStartEvent(plugin);

            // Verify the db operations fail
            Assert.Catch<InvalidOperationException>(() => plugin.Object.PruneDatabase());

            FireBackupStopEvent(plugin);

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            plugin.Object.PruneDatabase();
        }

        [Test]
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
            Assert.Catch<InvalidOperationException>(() => plugin.Object.PruneDatabase());

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            plugin.Object.PruneDatabase();
        }

        [Test]
        public void CanAccessDBFileAfterBackupStarts()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var hsMockController = TestHelper.SetupHsControllerAndSettings2(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);
            FireBackupStartEvent(plugin);

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
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

        [Test]
        public void PluginStatusDuringBackup()
        {
            SetupPlugin(out var plugin, out var mockHsController);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            FireBackupStartEvent(plugin);

            var statusBackup = plugin.Object.OnStatusCheck();
            Assert.That(statusBackup.Status, Is.EqualTo(EPluginStatus.Warning));
            Assert.That(statusBackup.StatusText, Is.EqualTo("Device records are not being stored"));

            FireBackupStopEvent(plugin);

            Assert.That(TestHelper.TimedWaitTillTrue(() =>
            {
                return EPluginStatus.Ok == plugin.Object.OnStatusCheck().Status;
            }));

            var status = plugin.Object.OnStatusCheck();
            Assert.That(status.Status, Is.EqualTo(EPluginStatus.Ok));
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