using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi.Database;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public IList<string> GetAllowedDisplays(string? refIdString)
        {
            var displays = new List<string>();

            if (string.IsNullOrEmpty(refIdString))
            {
                return displays;
            }

            int refId = ParseRefId(refIdString);
            AddToDisplayDetails(displays, refId);
            return displays;
        }

        public IDictionary<int, string> GetDeviceAndFeaturesNames(string refIdString)
        {
            var idNames = new Dictionary<int, string>();
            int refId = ParseRefId(refIdString);

            HashSet<int> featureRefIds;

            if (HomeSeerSystem.IsRefDevice(refId))
            {
                featureRefIds = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(refId, EProperty.AssociatedDevices);
            }
            else
            {
                featureRefIds = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(refId, EProperty.AssociatedDevices);

                var first = featureRefIds.First();
                featureRefIds = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(first, EProperty.AssociatedDevices);
            }

            featureRefIds.Add(refId);
            foreach (var featureRefId in featureRefIds)
            {
                idNames.Add(featureRefId, GetNameOfDevice(featureRefId));
            }

            return idNames;

            string GetNameOfDevice(int deviceRefId)
            {
                return HomeSeerSystem.GetNameByRef(deviceRefId).ToString() ?? Invariant($"RefId:{deviceRefId}");
            }
        }

        public long GetOldestRecordTimeDate(string refIdString)
        {
            int refId = ParseRefId(refIdString);
            var oldest = GetCollector().GetOldestRecordTimeDate(refId).ResultForSync<DateTimeOffset>();
            return (long)Math.Round((DateTimeOffset.Now - oldest).TotalSeconds);
        }

        public override bool HasJuiDeviceConfigPage(int devOrFeatRef)
        {
            return base.HasJuiDeviceConfigPage(devOrFeatRef);
        }

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            switch (page)
            {
                case "historyrecords":
                    return HandleHistoryRecords(data).ResultForSync();

                case "graphrecords":
                    return HandleGraphRecords(data).ResultForSync();
            }

            return base.PostBackProc(page, data, user, userRights);
        }

        private static string? GetTableValue(CultureInfo culture, object? column)
        {
            switch (column)
            {
                case double doubleValue:
                    return RoundDoubleValue(culture, doubleValue);

                case float floatValue:
                    return RoundDoubleValue(culture, floatValue);

                case TimeSpan span:
                    {
                        StringBuilder stringBuilder = new();

                        int days = span.Days;
                        if (days > 0)
                        {
                            stringBuilder.AppendFormat(culture, "{0} {1}", days, (days > 1 ? "days" : "day"));
                        }

                        int hours = span.Hours;
                        if (hours > 0)
                        {
                            if (stringBuilder.Length > 1)
                            {
                                stringBuilder.Append(' ');
                            }
                            stringBuilder.AppendFormat(culture, "{0} {1}", hours, (hours > 1 ? "hours" : "hour"));
                        }
                        int minutes = span.Minutes;
                        if (minutes > 0)
                        {
                            if (stringBuilder.Length > 1)
                            {
                                stringBuilder.Append(' ');
                            }
                            stringBuilder.AppendFormat(culture, "{0} {1}", minutes, (minutes > 1 ? "minutes" : "minute"));
                        }

                        int seconds = span.Seconds;
                        if (seconds > 0)
                        {
                            if (stringBuilder.Length > 1)
                            {
                                stringBuilder.Append(' ');
                            }
                            stringBuilder.AppendFormat(culture, "{0} {1}", seconds, (seconds > 1 ? "seconds" : "second"));
                        }

                        return stringBuilder.ToString();
                    }

                case null:
                    return null;

                case string stringValue:
                    if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedValue))
                    {
                        return RoundDoubleValue(culture, parsedValue);
                    }
                    return stringValue;

                default:
                    return Convert.ToString(column, culture);
            }

            static string RoundDoubleValue(CultureInfo culture, double floatValue)
            {
                return Math.Round(floatValue, 3, MidpointRounding.AwayFromZero).ToString("G", culture);
            }
        }

        private static TimeSpan ParseDuration(string duration)
        {
            var seconds = ParseInt("duration", duration);
            return TimeSpan.FromSeconds(seconds);
        }

        private static long ParseInt(string argumentName, string? refIdString)
        {
            if (long.TryParse(refIdString,
                             System.Globalization.NumberStyles.Any,
                             CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            else
            {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

        private static bool ShouldShowChartByDefault(HsFeature feature)
        {
            if (IsOnlyOnOffFeature(feature) && !HasAnyRangeGraphics(feature))
            {
                return false;
            }
            return true;

            static bool IsOnlyOnOffFeature(HsFeature feature)
            {
                return feature.StatusControls.Values.All(x => x.ControlUse == EControlUse.On || x.ControlUse == EControlUse.Off);
            }
            static bool HasAnyRangeGraphics(HsFeature feature)
            {
                return feature.StatusGraphics.Values.Any(x => x.IsRange);
            }
        }

        private void AddToDisplayDetails(IList<string> displayTypes, int refId)
        {
            var feature = HomeSeerSystem.GetFeatureByRef(refId);

            if (IsMonitored(feature))
            {
                displayTypes.Add("table");
                if (ShouldShowChartByDefault(feature))
                {
                    displayTypes.Add("chart");
                }
                // displayTypes.Add("averageStats");

                //if (hasNumericData)
                //{
                //    displayTypes.Add("averageStats");
                //}
                //else
                //{
                //    displayTypes.Add("histogram");
                //}
            }
        }

        private string CreateDeviceConfigPage(AbstractHsDevice device, string iFrameName)
        {
            StringBuilder stb = new();

            stb.Append("<script> $('#save_device_config').hide(); </script>");

            string iFrameUrl = Invariant($"{CreatePlugInUrl(iFrameName)}?refId={device.Ref}");

            // iframeSizer.min.js
            stb.Append($"<script type=\"text/javascript\" src=\"{CreatePlugInUrl("iframeResizer.min.js")}\"></script>");
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;min-height: 40rem; border: 0px;}</style>");
            stb.Append(Invariant($"<iframe id=\"historicalRecordsiFrame\" src=\"{iFrameUrl}\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{heightCalculationMethod: 'max', log: true, inPageLinks: true }}, '#historicalRecordsiFrame');</script>"));

            var page = PageFactory.CreateDeviceConfigPage(Id, "Device").WithLabel("id", stb.ToString());

            return page.Page.ToJsonString();
        }

        private string CreatePlugInUrl(string fileName)
        {
            return "/" + Id + "/" + fileName;
        }

        private static TimeSpan GetDefaultGroupInterval(TimeSpan duration)
        {
            // aim for 256 points on graph
            return TimeSpan.FromSeconds(duration.TotalSeconds / 256);
        }

        private async Task<string> HandleGraphRecords(string data)
        {
            StringBuilder stb = new();
            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();

            try
            {
                var collector = GetCollector();
                var jsonData = (JObject?)JsonConvert.DeserializeObject(data);

                var refId = jsonData?["refId"]?.Value<int>();
                var min = jsonData?["min"]?.Value<long>();
                var max = jsonData?["max"]?.Value<long>();
                if (refId == null || min == null || max == null)
                {
                    throw new ArgumentException("data is not correct");
                }

                long groupBySeconds = (long)GetDefaultGroupInterval(TimeSpan.FromMilliseconds(max.Value - min.Value)).TotalSeconds;
                bool shouldGroup = groupBySeconds >= 5;

                var queryData = await collector.GetGraphValues(refId.Value,
                                                               min.Value / 1000,
                                                               max.Value / 1000).ConfigureAwait(false);

                var queryDataGrouped = shouldGroup ?
                                            GroupValues(min.Value / 1000, max.Value / 1000, groupBySeconds, queryData) :
                                            queryData;

                jsonWriter.WritePropertyName("groupedbyseconds");
                jsonWriter.WriteValue(shouldGroup ? groupBySeconds : 0);

                jsonWriter.WritePropertyName("data");
                jsonWriter.WriteStartArray();

                foreach (var row in queryDataGrouped)
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("x");
                    jsonWriter.WriteValue(row.UnixTimeMilliSeconds);
                    jsonWriter.WritePropertyName("y");
                    jsonWriter.WriteValue(row.DeviceValue);
                    jsonWriter.WriteEndObject();
                }

                jsonWriter.WriteEndArray();
            }
            catch (Exception ex)
            {
                jsonWriter.WritePropertyName("error");
                jsonWriter.WriteValue(ex.GetFullMessage());
            }
            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();

            static IEnumerable<TimeAndValue> GroupValues(long min, long max, long groupBySeconds, IList<TimeAndValue> data)
            {
                var list = new TimeAndValueList(data);
                TimeSeriesHelper helper = new(min, max, groupBySeconds, list);
                return helper.ReduceSeriesWithAverageAndPreviousFill();
            }
        }

        private async Task<string> HandleHistoryRecords(string data)
        {
            StringBuilder stb = new();
            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();

            try
            {
                var collector = GetCollector();
                var parameters = HttpUtility.ParseQueryString(data);

                var refId = ParseParameterAsInt(parameters, "refId");
                var start = ParseParameterAsInt(parameters, "start");
                var length = ParseParameterAsInt(parameters, "length");
                var sortOrder = CalculateSortOrder(parameters["order[0][column]"], parameters["order[0][dir]"]);

                long totalResultsCount = 0;
                long recordLimit;

                long min;
                long max;
                if (!string.IsNullOrEmpty(parameters["min"]) && !string.IsNullOrEmpty(parameters["max"]))
                {
                    min = ParseParameterAsInt(parameters, "min") / 1000;
                    max = ParseParameterAsInt(parameters, "max") / 1000;
                    totalResultsCount = await collector.GetRecordsCount(refId, min, max).ConfigureAwait(false);
                    recordLimit = int.MaxValue;
                }
                else if (!string.IsNullOrEmpty(parameters["recordLimit"]))
                {
                    min = 0;
                    max = DateTimeOffset.Now.AddYears(100).ToUnixTimeSeconds(); //  a large future date
                    recordLimit = ParseParameterAsInt(parameters, "recordLimit");
                    totalResultsCount = recordLimit;
                }
                else
                {
                    throw new Exception("Niether min/max or recordCount specified");
                }

                var queryData = await collector.GetRecords(refId,
                                                           min,
                                                           max,
                                                           start,
                                                           Math.Min(length, recordLimit),
                                                           sortOrder).ConfigureAwait(false);

                jsonWriter.WritePropertyName("draw");
                jsonWriter.WriteValue(parameters["draw"]);

                jsonWriter.WritePropertyName("recordsTotal");
                jsonWriter.WriteValue(totalResultsCount);

                jsonWriter.WritePropertyName("recordsFiltered");
                jsonWriter.WriteValue(totalResultsCount);

                jsonWriter.WritePropertyName("data");
                jsonWriter.WriteStartArray();

                foreach (var row in queryData)
                {
                    jsonWriter.WriteStartArray();
                    jsonWriter.WriteValue(row.UnixTimeMilliSeconds);
                    jsonWriter.WriteValue(GetTableValue(CultureInfo.InvariantCulture, row.DeviceValue));
                    jsonWriter.WriteValue(GetTableValue(CultureInfo.InvariantCulture, row.DeviceString));
                    jsonWriter.WriteEndArray();
                }

                jsonWriter.WriteEndArray();
            }
            catch (Exception ex)
            {
                jsonWriter.WritePropertyName("error");
                jsonWriter.WriteValue(ex.GetFullMessage());
            }
            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();

            static ResultSortBy CalculateSortOrder(string? sortBy, string? sortDir)
            {
                switch (sortBy)
                {
                    case "0": return sortDir == "desc" ? ResultSortBy.TimeDesc : ResultSortBy.TimeAsc;
                    case "1": return sortDir == "desc" ? ResultSortBy.ValueDesc : ResultSortBy.ValueAsc;
                    case "2": return sortDir == "desc" ? ResultSortBy.StringDesc : ResultSortBy.StringAsc;
                }
                return ResultSortBy.TimeDesc;
            }

            static long ParseParameterAsInt(NameValueCollection parameters, string name)
            {
                return ParseInt(name, parameters[name]);
            }
        }
    }
}