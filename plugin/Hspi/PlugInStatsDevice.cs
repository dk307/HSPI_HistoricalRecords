#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Hspi.Device;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public string? GetStatisticDeviceDataAsJson(object refIdString)
        {
            var refId = Hspi.Utils.TypeConverter.TryGetFromObject<int>(refIdString)
                ?? throw new ArgumentException(null, nameof(refIdString));

            return sqliteManager?.GetStatisticDeviceDataAsJson(refId);
        }

        public List<int> GetTrackedDeviceList() => HomeSeerSystem.GetAllRefs().Where(id => IsFeatureTracked(id)).ToList();

        internal bool UpdateStatisticsFeature(int featureRefId) => sqliteManager?.TryUpdateStatisticDeviceData(featureRefId) ?? false;

        private static void ExtractDeviceOperationParameters(string data, out JObject jsonData, out StatisticsDeviceData statisticsDeviceData)
        {
            jsonData = ParseToJObject(data);
            var dataJObject = GetJsonValue<JObject>(jsonData, "data");
            JsonSerializer serializer = new();
            statisticsDeviceData = serializer.Deserialize<StatisticsDeviceData>(new JTokenReader(dataJObject)) ??
                                       throw new ArgumentException("data is incorrect", nameof(data));
        }

        private static JObject ParseToJObject(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                throw new ArgumentException("data is not correct");
            }

            var deserializedObject = JObject.Parse(data);
            return deserializedObject ?? throw new ArgumentException("data is not correct");
        }

        private static string SendRefIdResult(int refId)
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
            jsonWriter.WritePropertyName("ref");
            jsonWriter.WriteValue(refId);
            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();
        }

        private string HandleDeleteRecords(string data)
        {
            var jsonData = ParseToJObject(data);
            var refId = GetJsonValue<int>(jsonData, "ref");

            DeleteAllRecords(refId);
            return "{}";
        }

        private string HandleDeviceCreate(string data)
        {
            ExtractDeviceOperationParameters(data, out var jsonData, out var statisticsDeviceData);

            var name = GetJsonValue<string>(jsonData, "name");
            var refId = StatisticsDevice.CreateDevice(HomeSeerSystem, name, statisticsDeviceData);

            sqliteManager?.RestartStatisticsDeviceUpdate();

            return SendRefIdResult(refId);
        }

        private string HandleDeviceEdit(string data)
        {
            ExtractDeviceOperationParameters(data, out var jsonData, out var statisticsDeviceData);

            var refId = GetJsonValue<int>(jsonData, "ref");
            StatisticsDevice.EditDevice(HomeSeerSystem, refId, statisticsDeviceData);

            sqliteManager?.RestartStatisticsDeviceUpdate();

            return SendRefIdResult(refId);
        }

        private string HandleExecSql(string data)
        {
            var jsonData = ParseToJObject(data);
            var sql = GetJsonValue<string>(jsonData, "sql");

            var result = Collector.ExecSql(sql);

            return WriteJsonResult((jsonWriter) =>
            {
                jsonWriter.WritePropertyName("columns");
                jsonWriter.WriteStartArray();
                if (result.Count > 0)
                {
                    foreach (var col in result[0])
                    {
                        jsonWriter.WriteValue(col.Key);
                    }
                }

                jsonWriter.WriteEndArray();

                jsonWriter.WritePropertyName("data");
                jsonWriter.WriteStartArray();

                foreach (var row in result)
                {
                    jsonWriter.WriteStartArray();
                    foreach (var col in row)
                    {
                        jsonWriter.WriteValue(col.Value);
                    }

                    jsonWriter.WriteEndArray();
                }

                jsonWriter.WriteEndArray();
            });
        }
    }
}