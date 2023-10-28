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
using Serilog;

namespace Hspi
{
    public enum StatisticsFunction
    {
        AverageStep = 0,
        AverageLinear = 1,
    };

    internal partial class PlugIn : HspiBase
    {
        public string? GetStatisticDeviceDataAsJson(object refIdString)
        {
            var refId = Hspi.Utils.TypeConverter.TryGetFromObject<int>(refIdString)
                ?? throw new ArgumentException(null, nameof(refIdString));

            return StatisticsDevice.GetDataFromFeatureAsJson(HomeSeerSystem, refId);
        }

        public List<int> GetTrackedDeviceList()
        {
            return HomeSeerSystem.GetAllRefs().Where(id => IsFeatureTracked(id)).ToList();
        }

        public bool UpdateStatisticsFeature(int featureRefId)
        {
            return statisticsDeviceUpdater?.UpdateData(featureRefId) ?? false;
        }

        private static string SendRefIdResult(int refId)
        {
            StringBuilder stb = new();
            var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            var jsonWriter = new JsonTextWriter(stringWriter)
            {
                Formatting = Formatting.Indented
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

        private string HandleDeviceCreate(string data)
        {
            var jsonData = (JObject?)JsonConvert.DeserializeObject(data);
            var name = GetJsonValue<string>(jsonData, "name");
            var dataJObject = GetJsonValue<JObject>(jsonData, "data");
            JsonSerializer serializer = new();
            var statisticsDeviceData = serializer.Deserialize<StatisticsDeviceData>(new JTokenReader(dataJObject)) ??
                                       throw new ArgumentException("data is incorrect", nameof(data));

            var refId = StatisticsDevice.CreateDevice(HomeSeerSystem, name, statisticsDeviceData);

            RestartStatisticsDeviceUpdate();

            return SendRefIdResult(refId);
        }

        private string HandleDeviceEdit(string data)
        {
            var jsonData = (JObject?)JsonConvert.DeserializeObject(data);
            var refId = GetJsonValue<int>(jsonData, "ref");
            var dataJObject = GetJsonValue<JObject>(jsonData, "data");
            JsonSerializer serializer = new();
            var statisticsDeviceData = serializer.Deserialize<StatisticsDeviceData>(new JTokenReader(dataJObject)) ??
                                       throw new ArgumentException(nameof(data));

            StatisticsDevice.EditDevice(HomeSeerSystem, refId, statisticsDeviceData);

            RestartStatisticsDeviceUpdate();

            return SendRefIdResult(refId);
        }

        private void RestartStatisticsDeviceUpdate()
        {
            Log.Debug("Restarting statistics device update");
            CheckNotNull(featureCachedDataProvider);
            statisticsDeviceUpdater?.Dispose();
            statisticsDeviceUpdater = new StatisticsDeviceUpdater(HomeSeerSystem, Collector, CreateClock(), featureCachedDataProvider, ShutdownCancellationToken);
        }

        private StatisticsDeviceUpdater? statisticsDeviceUpdater;
    }
}