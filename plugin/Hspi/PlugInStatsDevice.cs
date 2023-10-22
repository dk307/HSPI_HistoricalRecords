#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Hspi.DeviceData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Hspi

{
    public enum StatisticsFunction
    {
        [Description("averagestep")]
        AverageStep = 0,

        [Description("averagelinear")]
        AverageLinear = 1,
    };

    internal partial class PlugIn : HspiBase
    {
        public List<int> GetRecordedDeviceList()
        {
            return HomeSeerSystem.GetAllRefs().Where(id => IsFeatureTracked(id)).ToList();
        }

        private string HandleDeviceCreate(string data)
        {
            var jsonData = (JObject?)JsonConvert.DeserializeObject(data);

            var trackedref = GetJsonValue<int>(jsonData, "trackedref");
            var function = GetStatisticsFunction(jsonData);
            var daysDuration = GetJsonValue<int>(jsonData, "daysDuration");
            var hoursDuration = GetJsonValue<int>(jsonData, "hoursDuration");
            var minutesDuration = GetJsonValue<int>(jsonData, "minutesDuration");
            var secondsDuration = GetJsonValue<int>(jsonData, "secondsDuration");
            var daysRefresh = GetJsonValue<int>(jsonData, "daysRefresh");
            var hoursRefresh = GetJsonValue<int>(jsonData, "hoursRefresh");
            var minutesRefresh = GetJsonValue<int>(jsonData, "minutesRefresh");
            var secondsRefresh = GetJsonValue<int>(jsonData, "secondsRefresh");

            var durationInterval = GetDuration(daysDuration, hoursDuration, minutesDuration, secondsDuration);
            var refreshInterval = GetDuration(daysRefresh, hoursRefresh, minutesRefresh, secondsRefresh);

            var refId = StatisticsDevice.CreateDevice(HomeSeerSystem,
                                                      new StatisticsDeviceData(trackedref, function, durationInterval, refreshInterval));

            RestartStatisticsDeviceUpdate();

            StringBuilder stb = new();
            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("result");
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("ref");
            jsonWriter.WriteValue(refId);
            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();

            static TimeSpan GetDuration(int days, int hours, int minutes, int seconds)
            {
                TimeSpan timeSpan = new(days, hours, minutes, seconds);
                return timeSpan;
            }

            static StatisticsFunction GetStatisticsFunction(JObject? jsonData)
            {
                var str = GetJsonValue<string>(jsonData, "function");
                return GetStatisticsFunctionFromString(str);
            }
        }

        private void RestartStatisticsDeviceUpdate()
        {
            Log.Debug("Restarting statistics device update");
            statisticsDeviceUpdater?.Dispose();
            statisticsDeviceUpdater = new StatisticsDeviceUpdater(HomeSeerSystem, Collector, CreateClock(), ShutdownCancellationToken);
        }

        private static StatisticsFunction GetStatisticsFunctionFromString(string str)
        {
            return str switch
            {
                "averagestep" => StatisticsFunction.AverageStep,
                "averagelinear" => StatisticsFunction.AverageLinear,
                _ => throw new ArgumentException("function is not correct"),
            };
        }

        private StatisticsDeviceUpdater? statisticsDeviceUpdater;
    }
}