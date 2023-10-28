using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class DeleteDataTest
    {
        [TestMethod]
        public void DeleteOrphanDataOnStart()
        {
            var plugin1 = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings2(plugin1);

            int refId = 1000;

            mockHsController.SetupFeature(refId, 1.1);
            using (PlugInLifeCycle plugInLifeCycle1 = new(plugin1))
            {
                Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin1, refId, 1));
            }

            mockHsController.RemoveFeatureOrDevice(refId);
            var plugin2 = TestHelper.CreatePlugInMock();
            TestHelper.UpdatePluginHsGet(plugin2, mockHsController);

            using PlugInLifeCycle plugInLifeCycle2 = new(plugin2);
            Assert.IsTrue(TestHelper.WaitTillTotalRecords(plugin2, refId, 0));
        }
    }
}