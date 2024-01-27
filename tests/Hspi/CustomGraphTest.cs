using System.Collections.Generic;
using System.Drawing;
using Hspi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class CustomGraphTest
    {
        [Test]
        public void AddGraph()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            string name = "My graph";

            //add
            int id = AddGraph(plugIn, name);
            Assert.That(id, Is.EqualTo(1));

            var graphs = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs.Count, Is.EqualTo(1));
            Assert.That(graphs[0]["id"], Is.EqualTo(1));
            Assert.That(graphs[0]["name"], Is.EqualTo(name));
            Assert.That(((Dictionary<int, Dictionary<string, object>>)graphs[0]["lines"]).Count, Is.EqualTo(0));
        }

        [TestCase("{\"name\":\"\"}", "name is empty")]
        [TestCase("{}", "name is not correct")]
        [TestCase("", "data is not correct")]
        public void AddGraphErrorChecking(string format, string exception)
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            //add
            string data = plugIn.Object.PostBackProc("graphcreate", format, string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var errorMessage = jsonData["error"].Value<string>();
            Assert.That(!string.IsNullOrWhiteSpace(errorMessage));
            Assert.That(errorMessage, Does.Contain(exception));
        }

        [Test]
        public void AddGraphLine()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            int graphid = AddGraph(plugIn, "g1");

            int refId = 100;
            string color = "fff";

            AddGraphLine(plugIn, graphid, refId, color);

            var graphs = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs.Count, Is.EqualTo(1));
            var lines = ((Dictionary<int, Dictionary<string, object>>)graphs[0]["lines"]);
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[1]["refId"], Is.EqualTo(refId));
            Assert.That(lines[1]["lineColor"], Is.EqualTo(color));
        }

        [Test]
        public void AddMultipleGraphLine()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            int graphid = AddGraph(plugIn, "g1");

            AddGraphLine(plugIn, graphid, 100, "3");
            AddGraphLine(plugIn, graphid, 102, "6");
            AddGraphLine(plugIn, graphid, 103, "5");
            AddGraphLine(plugIn, graphid, 105, "4");

            var graphs = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs.Count, Is.EqualTo(1));
            var lines = ((Dictionary<int, Dictionary<string, object>>)graphs[0]["lines"]);
            Assert.That(lines.Count, Is.EqualTo(4));
            Assert.That(lines[4]["refId"], Is.EqualTo(105));
            Assert.That(lines[4]["lineColor"], Is.EqualTo("4"));
        }

        [Test]
        public void AddMultipleGraphs()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            //add
            int id1 = AddGraph(plugIn, "1");
            int id2 = AddGraph(plugIn, "2");
            int id3 = AddGraph(plugIn, "3");

            var graphs = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs.Count, Is.EqualTo(3));
            Assert.That(graphs[0]["id"], Is.EqualTo(1));
            Assert.That(graphs[1]["id"], Is.EqualTo(2));
            Assert.That(graphs[2]["id"], Is.EqualTo(3));
        }

        [Test]
        public void DeleteGraph()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            //add
            int id = AddGraph(plugIn, "g1");

            //delete
            string data = plugIn.Object.PostBackProc("graphdelete", $"{{\"id\":\"{id}\"}}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var errorMessage = jsonData["error"];
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void DeleteGraphLine()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            int graphid = AddGraph(plugIn, "g1");

            AddGraphLine(plugIn, graphid, 100, "e");

            var graphs = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs.Count, Is.EqualTo(1));
            var lines = ((Dictionary<int, Dictionary<string, object>>)graphs[0]["lines"]);
            Assert.That(lines.Count, Is.EqualTo(1));

            //delete
            string data = plugIn.Object.PostBackProc("graphlinedelete", $"{{\"graphid\":\"{graphid}\", \"graphlineid\":\"{1}\"}}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var graphs2 = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs2.Count, Is.EqualTo(1));
            var lines2 = ((Dictionary<int, Dictionary<string, object>>)graphs2[0]["lines"]);
            Assert.That(lines2.Count, Is.EqualTo(0));
        }

        [Test]
        public void EditGraphLine()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            int graphid = AddGraph(plugIn, "g1");

            AddGraphLine(plugIn, graphid, 100, "e");

            //edit
            string data = plugIn.Object.PostBackProc("graphlineedit", $"{{\"graphid\":\"{graphid}\", \"graphlineid\":\"{1}\", \"refid\":\"{999}\", \"linecolor\":\"newColor\"}}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var graphs = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs.Count, Is.EqualTo(1));
            var lines = ((Dictionary<int, Dictionary<string, object>>)graphs[0]["lines"]);
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[1]["refId"], Is.EqualTo(999));
            Assert.That(lines[1]["lineColor"], Is.EqualTo("newColor"));
        }

        [Test]
        public void RenameGraph()
        {
            var plugIn = TestHelper.CreatePlugInMock();

            TestHelper.SetupHsControllerAndSettings2(plugIn);

            using PlugInLifeCycle plugInLifeCycle = new(plugIn);

            //add
            int id = AddGraph(plugIn, "g1");
            string newName = "new bane";

            //rename
            string data = plugIn.Object.PostBackProc("graphedit", $"{{\"id\":\"{id}\", \"name\":\"{newName}\", }}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);

            var errorMessage = jsonData["error"];
            Assert.That(errorMessage, Is.Null);

            var graphs = plugIn.Object.GetCustomGraphs();
            Assert.That(graphs.Count, Is.EqualTo(1));
            Assert.That(graphs[0]["id"], Is.EqualTo(1));
            Assert.That(graphs[0]["name"], Is.EqualTo(newName));
            Assert.That(((Dictionary<int, Dictionary<string, object>>)graphs[0]["lines"]).Count, Is.EqualTo(0));
        }

        private static int AddGraph(Mock<PlugIn> plugIn, string name)
        {
            string data = plugIn.Object.PostBackProc("graphcreate", $"{{\"name\":\"{name}\"}}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);
            var id = jsonData["result"]["id"].Value<int>();

            var errorMessage = jsonData["error"];
            Assert.That(errorMessage, Is.Null);

            return id;
        }

        private static void AddGraphLine(Mock<PlugIn> plugIn, int graphid, int refId, string color)
        {
            string data = plugIn.Object.PostBackProc("graphlinecreate", $"{{\"graphid\":\"{graphid}\", \"refid\":\"{refId}\", \"linecolor\":\"{color}\"}}", string.Empty, 0);
            Assert.That(data, Is.Not.Null);

            var jsonData = (JObject)JsonConvert.DeserializeObject(data);
            Assert.That(jsonData, Is.Not.Null);
        }
    }
}