using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Hspi;
using Hspi.Graph;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class CustomGraphManagerTest
    {
        [Test]
        public void ValuesArePersistedForAdd()
        {
            var tempFile = Path.GetTempFileName();

            CustomGraphManager instance = new(tempFile);

            var graph = instance.CreateGraph("name1");
            graph = instance.AddGraphLine(graph.Id, new CustomGraphLine(100, "d"));

            // read from disk directly
            CustomGraphManager instance2 = new(tempFile);

            Assert.That(instance2.Graphs, Has.Count.EqualTo(1));
            Assert.That(instance2.Graphs[1].Id, Is.EqualTo(graph.Id));
            Assert.That(instance2.Graphs[1].Name, Is.EqualTo(graph.Name));
            Assert.That(instance2.Graphs[1].Lines, Is.EqualTo(graph.Lines));
        }

        [Test]
        public void ValuesArePersistedAfterChanges()
        {
            var tempFile = Path.GetTempFileName();

            CustomGraphManager instance = new(tempFile);

            var graph = instance.CreateGraph("name1");
            graph = instance.AddGraphLine(graph.Id, new CustomGraphLine(100, "d"));
            graph = instance.AddGraphLine(graph.Id, new CustomGraphLine(120, "2d"));
            graph = instance.AddGraphLine(graph.Id, new CustomGraphLine(120, "2d"));

            graph = instance.UpdateGraph(graph.Id, "r");
            graph = instance.UpdateGraphLine(graph.Id, 1, new CustomGraphLine(33, "555"));
            graph = instance.DeleteGraphLine(graph.Id, 2);

            // read from disk directly
            CustomGraphManager instance2 = new(tempFile);

            Assert.That(instance2.Graphs, Has.Count.EqualTo(1));
            Assert.That(instance2.Graphs[1].Id, Is.EqualTo(graph.Id));
            Assert.That(instance2.Graphs[1].Name, Is.EqualTo(graph.Name));
            Assert.That(instance2.Graphs[1].Lines, Is.EqualTo(graph.Lines));
        }

        [Test]
        public void ValuesArePersistedAfterDelete()
        {
            var tempFile = Path.GetTempFileName();

            CustomGraphManager instance = new(tempFile);

            var graph = instance.CreateGraph("name1");

            instance.DeleteGraph(graph.Id);

            // read from disk directly
            CustomGraphManager instance2 = new(tempFile);

            Assert.That(instance2.Graphs, Has.Count.EqualTo(0));
        }
    }
}