using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class GraphCallbacks
    {
        [TestMethod]
        [DataTestMethod]
        [DataRow("", "refId is not correct")]
        [DataRow("refId={0}&min=1001&max=99", "Unexpected character encountered")]
        [DataRow("{{ refId:{0}, min:1001, max:99}}", "max < min")]
        [DataRow("{{ refId:{0}, min:'abc', max:99 }}", "min is not correct")]
        [DataRow("{{ refId:{0}, min:33, max:'abc' }}", "max is not correct")]
        [DataRow("{{refId:{0}}}", "min is not correct")]
        [DataRow("{{refId1:{0}}}", "refId is not correct")]
        public void GraphCallbackArgumentChecks(string format, string exception)
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            Assert.IsTrue(plugin.Object.InitIO());

            int devRefId = 1938;
            string paramsForRecord = String.Format(format, devRefId);

            string data = plugin.Object.PostBackProc("graphrecords", paramsForRecord, string.Empty, 0);
            Assert.IsNotNull(data);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.IsNotNull(jsonData);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, exception);

            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
        }
    }
}