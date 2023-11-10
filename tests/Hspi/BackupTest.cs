using System;
using System.IO;
using HomeSeer.PluginSdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static HomeSeer.PluginSdk.PluginStatus;

namespace HSPI_HistoricalRecordsTest

{
    [TestClass]
    public class BackupTest
    {
        [TestMethod]
        public void BackupDatabaseDoesNotLooseChanges()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);

            int deviceRefId = 1000;
            mockHsController.SetupFeature(deviceRefId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            TestHelper.WaitForRecordCountAndDeleteAll(plugin, deviceRefId, 1);

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 1 });

            TestHelper.RaiseHSEvent(plugin, Constants.HSEvent.VALUE_CHANGE, deviceRefId);

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 2 });

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1));
        }

        [TestMethod]
        public void BackupEndMultipleTimes()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin);

            int deviceRefId = 1000;
            mockHsController.SetupFeature(deviceRefId, 100);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin, deviceRefId, 1));

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 1 });

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 2 });
            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 2 });

            Assert.AreEqual(1, plugin.Object.GetTotalRecords(deviceRefId));
        }

        [TestMethod]
        public void BackupStopsDatabase()
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings2(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 1 });

            // Verify the db operations fail
            Assert.ThrowsException<InvalidOperationException>(() => plugin.Object.PruneDatabase());

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 2 });

            plugin.Object.PruneDatabase();
        }

        [TestMethod]
        public void CanAccessDBFileAfterBackupStarts()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var hsMockController = TestHelper.SetupHsControllerAndSettings2(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 1 });

            File.Delete(hsMockController.DBPath);
            Assert.IsFalse(File.Exists(hsMockController.DBPath));
        }

        [TestMethod]
        public void PluginStatusDuringBackup()
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings2(plugin);

            using PlugInLifeCycle plugInLifeCycle = new(plugin);

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 1 });

            var statusBackup = plugin.Object.OnStatusCheck();
            Assert.AreEqual(EPluginStatus.Warning, statusBackup.Status);
            Assert.AreEqual("Device records are not being stored", statusBackup.StatusText);

            plugin.Object.HsEvent(BackUpEvent, new object[] { 512, 2 });

            var status = plugin.Object.OnStatusCheck();
            Assert.AreEqual(EPluginStatus.Ok, status.Status);
        }

        private const Constants.HSEvent BackUpEvent = (Constants.HSEvent)0x200;
    }
}