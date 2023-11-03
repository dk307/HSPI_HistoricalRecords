﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HomeSeer.PluginSdk.Devices;
using Hspi.Device;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public string? GetStatisticDeviceDataAsJson(object refIdString)
        {
            var refId = Hspi.Utils.TypeConverter.TryGetFromObject<int>(refIdString)
                ?? throw new ArgumentException(null, nameof(refIdString));

            if (HomeSeerSystem.IsRefDevice(refId))
            {
                // find child
                var children = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(refId, EProperty.AssociatedDevices);
                if (children.Count == 1)
                {
                    refId = children.First();
                }
                else
                {
                    throw new HsDeviceInvalidException($"{refId} has invalid number of features({children.Count})");
                }
            }

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

        private static JObject ParseToJObject(string data)
        {
            return (JObject?)JsonConvert.DeserializeObject(data) ?? throw new ArgumentException("data is not correct");
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
            var jsonData = ParseToJObject(data);
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
            var jsonData = ParseToJObject(data);
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