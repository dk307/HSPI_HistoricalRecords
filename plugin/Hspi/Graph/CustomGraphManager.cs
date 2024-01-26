using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Serilog;

#nullable enable

namespace Hspi.Graph
{
    internal sealed class CustomGraphManager
    {
        public CustomGraphManager(string filePath)
        {
            this.filePath = filePath;

            if (File.Exists(filePath))
            {
                using StreamReader file = new(filePath, Encoding.UTF8);
                JsonSerializer serializer = new();
                var dict = (Dictionary<int, CustomGraph>?)serializer.Deserialize(file, typeof(Dictionary<int, CustomGraph>));
                graphs = dict?.ToImmutableDictionary() ?? ImmutableDictionary<int, CustomGraph>.Empty;
            }
            else
            {
                graphs = ImmutableDictionary<int, CustomGraph>.Empty;
            }

            Log.Information("Loaded {count} graph from settings", graphs.Count);
        }

        public ImmutableDictionary<int, CustomGraph> Graphs => graphs;

        private int MaxId => graphs.Count == 0 ? 0 : graphs.Select(x => x.Value.Id).Max();

        public CustomGraph AddGraphLine(int graphId, CustomGraphLine customGraphLine)
        {
            lock (graphsLock)
            {
                if (!graphs.TryGetValue(graphId, out var customGraph))
                {
                    throw new ArgumentException("graphId is invalid");
                }

                int maxId = customGraph.Lines.Count == 0 ? 0 : customGraph.Lines.Select(x => x.Key).Max();

                var lineBuilder = customGraph.Lines.ToBuilder();
                lineBuilder.Add(maxId + 1, customGraphLine);
                var newValue = customGraph with { Lines = lineBuilder.ToImmutableDictionary() };

                AddOrUpdate(newValue);
                return newValue;
            }
        }

        public CustomGraph DeleteGraphLine(int graphId, int graphLineId)
        {
            lock (graphsLock)
            {
                if (!graphs.TryGetValue(graphId, out var customGraph))
                {
                    throw new ArgumentException("graphId is invalid");
                }

                var lineBuilder = customGraph.Lines.ToBuilder();
                lineBuilder.Remove(graphLineId);
                var newValue = customGraph with { Lines = lineBuilder.ToImmutableDictionary() };

                AddOrUpdate(newValue);
                return newValue;
            }
        }

        public CustomGraph CreateGraph(string name)
        {
            lock (graphsLock)
            {
                var builder = graphs.ToBuilder();

                CustomGraph graph = new(MaxId + 1, name, ImmutableDictionary<int, CustomGraphLine>.Empty);
                AddOrUpdate(graph);
                return graph;
            }
        }

        public void DeleteGraph(int id)
        {
            lock (graphsLock)
            {
                var builder = graphs.ToBuilder();
                builder.Remove(id);
                graphs = builder.ToImmutableDictionary();
                Save();
            }
        }

        private void AddOrUpdate(CustomGraph newValue)
        {
            var builder = graphs.ToBuilder();
            builder[newValue.Id] = newValue;
            graphs = builder.ToImmutableDictionary();
            Save();
        }

        private void Save()
        {
            using StreamWriter file = new(filePath, false, Encoding.UTF8);
            JsonSerializer serializer = new()
            {
                Formatting = Formatting.Indented
            };

            serializer.Serialize(file, graphs);

            Log.Information("Saved {count} graphs to settings", graphs.Count);
        }

        private readonly string filePath;
        private readonly object graphsLock = new();
        private volatile ImmutableDictionary<int, CustomGraph> graphs;
    }
}