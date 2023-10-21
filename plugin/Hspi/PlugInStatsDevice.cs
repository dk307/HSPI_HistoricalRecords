#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Humanizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hspi

{
    internal enum StatisticsFunction
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

            return "{}";

            static TimeSpan GetDuration(int days, int hours, int minutes, int seconds)
            {
                TimeSpan timeSpan = new(days, hours, minutes, seconds);
                return timeSpan;
            }

            static StatisticsFunction GetStatisticsFunction(JObject? jsonData)
            {
                try
                {
                    var str = GetJsonValue<string>(jsonData, "function");
                    return str.DehumanizeTo<StatisticsFunction>();
                }
                catch (NoMatchFoundException ex)
                {
                    throw new ArgumentException("function is not correct", ex);
                }
            }
        }
    }
}