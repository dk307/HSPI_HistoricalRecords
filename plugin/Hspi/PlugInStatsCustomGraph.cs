#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Hspi.Graph;
using Newtonsoft.Json;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public List<Dictionary<string, object>> GetCustomGraphs()
        {
            return CustomGraphManager.Graphs.Select(x => create(x.Value)).ToList();

            static Dictionary<string, object> create(CustomGraph graph)
            {
                var data = new Dictionary<string, object>()
                {
                    { "id", graph.Id },
                    { "name", graph.Name },
                    { "lines", create(graph.Lines) }
                };

                return data;

                static Dictionary<int, Dictionary<string, object>> create(IReadOnlyDictionary<int, CustomGraphLine> graphLines)
                {
                    Dictionary<int, Dictionary<string, object>> result = [];
                    foreach (var entry in graphLines)
                    {
                        result[entry.Key] = new Dictionary<string, object>
                        {
                            { "refId", entry.Value.FeatureRefId  },
                            { "lineColor", entry.Value.LineColor }
                        };
                    }

                    return result;
                }
            }
        }

        // used by scrbian
        public List<int> GetDeviceListWithGraphAllowed()
        {
            return HomeSeerSystem.GetAllRefs().Where(x =>
            {
                if (!IsFeatureTracked(x))
                {
                    return false;
                }

                var feature = new HsFeatureData(HomeSeerSystem, x);

                if (!ShouldShowChart(feature))
                {
                    return false;
                }

                return true;
            }).ToList();
        }

        private static string SendCustomGraphIdResult(int id)
        {
            StringBuilder stb = new();
            var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            var jsonWriter = new JsonTextWriter(stringWriter)
            {
                Formatting = Formatting.None
            };
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("result");
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("id");
            jsonWriter.WriteValue(id);
            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();
        }

        private string HandleGraphCreate(string data)
        {
            var jsonData = ParseToJObject(data);
            var name = GetJsonValue<string>(jsonData, "name");

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name is empty");
            }

            CustomGraph customGraph = CustomGraphManager.CreateGraph(name);
            return SendCustomGraphIdResult(customGraph.Id);
        }

        private string HandleGraphLineCreate(string data)
        {
            var jsonData = ParseToJObject(data);
            var graphid = GetJsonValue<int>(jsonData, "graphid");
            var refid = GetJsonValue<int>(jsonData, "refid");
            var color = GetJsonValue<string>(jsonData, "linecolor");

            if (string.IsNullOrWhiteSpace(color))
            {
                throw new ArgumentException("color is empty");
            }

            CustomGraph customGraph = CustomGraphManager.AddGraphLine(graphid, new CustomGraphLine(refid, color));
            return SendCustomGraphIdResult(customGraph.Id);
        }

        private string HandleGraphLineDelete(string data)
        {
            var jsonData = ParseToJObject(data);
            var graphid = GetJsonValue<int>(jsonData, "graphid");
            var graphlineid = GetJsonValue<int>(jsonData, "graphlineid");

            CustomGraph customGraph = CustomGraphManager.DeleteGraphLine(graphid, graphlineid);
            return SendCustomGraphIdResult(customGraph.Id);
        }
    }
}