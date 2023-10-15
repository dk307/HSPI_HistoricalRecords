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
using Serilog;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public IList<string> GetAllowedDisplays(string? refIdString)
        {
            var displays = new List<string>();

            if (string.IsNullOrWhiteSpace(refIdString))
            {
                return displays;
            }

            var refId = ParseRefId(refIdString);
            var feature = HomeSeerSystem.GetFeatureByRef(refId);

            displays.Add("table");
            if (ShouldShowChart(feature))
            {
                displays.Add("chart");
            }

            return displays;
        }

        public List<int> GetDeviceAndFeaturesRefIds(string refIdString)
        {
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

            return featureRefIds.ToList();
        }

        public IList<long> GetEarliestAndOldestRecordTotalSeconds(string refIdString)
        {
            int refId = ParseRefId(refIdString);
            var data = Collector.GetEarliestAndOldestRecordTimeDate(refId).ResultForSync();

            var now = CreateClock().Now;

            return new List<long>() {
                (long)Math.Round((now - data.Item1).TotalSeconds),
                (long)Math.Round((now - data.Item2).TotalSeconds)
                };
        }

        public string? GetFeatureUnit(string refIdString)
        {
            int refId = ParseRefId(refIdString);

            var validUnits = new List<string>()
            {
                " Watts", " W",
                " kWh", " kW Hours",
                " Volts", " V",
                " vah",
                " F", " C", " K", "°F", "°C", "°K",
                " lux", " lx",
                " %",
                " A",
                " ppm", " ppb",
                " db", " dbm",
                " μs", " ms", " s", " min",
                " g", "kg", " mg", " uq", " oz", " lb",
            };

            //  an ugly way to get unit, but there is no universal way to get them in HS4
            var displayStatus = (string)HomeSeerSystem.GetPropertyByRef(refId, EProperty.DisplayedStatus);
            var unit = validUnits.Find(x => displayStatus.EndsWith(x, StringComparison.OrdinalIgnoreCase));
            return unit?.Substring(1);
        }

        public long GetTotalRecords(long refId)
        {
            var count = Collector.GetRecordsCount(refId, 0, long.MaxValue).ResultForSync<long>();
            return count;
        }

        public bool IsDeviceTracked(string refIdString)
        {
            int refId = ParseRefId(refIdString);
            CheckNotNull(settingsPages);
            return settingsPages.IsTracked(refId);
        }

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            return page switch
            {
                "historyrecords" => HandleHistoryRecords(data).ResultForSync(),
                "graphrecords" => HandleGraphRecords(data).ResultForSync(),
                "updatedevicesettings" => HandleUpdateDeviceSettings(data),
                _ => base.PostBackProc(page, data, user, userRights),
            };
        }

        protected virtual ISystemClock CreateClock() => new SystemClock();

        private static TimeSpan GetDefaultGroupInterval(TimeSpan duration)
        {
            // aim for 256 points on graph
            return TimeSpan.FromSeconds(duration.TotalSeconds / 256);
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

        private static bool ShouldShowChart(HsFeature feature)
        {
            if (IsOnlyOnOffFeature(feature) && !HasAnyRangeGraphics(feature))
            {
                return false;
            }
            return true;

            static bool IsOnlyOnOffFeature(HsFeature feature)
            {
                return feature.StatusControls.Values.TrueForAll(x => x.ControlUse == EControlUse.On || x.ControlUse == EControlUse.Off);
            }
            static bool HasAnyRangeGraphics(HsFeature feature)
            {
                return feature.StatusGraphics.Values.Exists(x => x.IsRange);
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

            string CreatePlugInUrl(string fileName)
            {
                return "/" + Id + "/" + fileName;
            }
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

                var queryData = await Collector.GetGraphValues(refId.Value,
                                                               min.Value / 1000,
                                                               max.Value / 1000).ConfigureAwait(false);

                var queryDataGrouped = shouldGroup ?
                                            GroupValues(min.Value / 1000, max.Value / 1000, groupBySeconds, queryData) :
                                            queryData;

                jsonWriter.WritePropertyName("result");
                jsonWriter.WriteStartObject();
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
                jsonWriter.WriteEndObject();
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
            Log.Debug("HandleHistoryRecords {data}", data);

            StringBuilder stb = new();
            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();

            try
            {
                var parameters = HttpUtility.ParseQueryString(data);

                var refId = ParseParameterAsInt(parameters, "refId");
                var start = ParseParameterAsInt(parameters, "start");
                var length = ParseParameterAsInt(parameters, "length");
                var sortOrder = CalculateSortOrder(parameters["order[0][column]"], parameters["order[0][dir]"]);

                long totalResultsCount = 0;

                long min;
                long max;
                if (!string.IsNullOrEmpty(parameters["min"]) && !string.IsNullOrEmpty(parameters["max"]))
                {
                    min = ParseParameterAsInt(parameters, "min") / 1000;
                    max = ParseParameterAsInt(parameters, "max") / 1000;

                    if (max < min)
                    {
                        throw new ArgumentException("max < min");
                    }

                    totalResultsCount = await Collector.GetRecordsCount(refId, min, max).ConfigureAwait(false);
                }
                else
                {
                    throw new ArgumentException("min/max not specified");
                }

                var queryData = await Collector.GetRecords(refId,
                                                           min,
                                                           max,
                                                           start,
                                                           length,
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
                    jsonWriter.WriteValue(row.DeviceValue);
                    jsonWriter.WriteValue(row.DeviceString);
                    jsonWriter.WriteValue(row.DurationSeconds);
                    jsonWriter.WriteEndArray();
                }

                jsonWriter.WriteEndArray();
            }
            catch (Exception ex)
            {
                Log.Error("Getting Records failed for {param} with {error}", data, ex.GetFullMessage());
                jsonWriter.WritePropertyName("error");
                jsonWriter.WriteValue(ex.GetFullMessage());
            }
            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();

            static ResultSortBy CalculateSortOrder(string? sortBy, string? sortDir)
            {
                return sortBy switch
                {
                    "0" => sortDir == "desc" ? ResultSortBy.TimeDesc : ResultSortBy.TimeAsc,
                    "1" => sortDir == "desc" ? ResultSortBy.ValueDesc : ResultSortBy.ValueAsc,
                    "2" => sortDir == "desc" ? ResultSortBy.StringDesc : ResultSortBy.StringAsc,
                    "3" => sortDir == "desc" ? ResultSortBy.DurationDesc : ResultSortBy.DurationAsc,
                    _ => ResultSortBy.TimeDesc,
                };
            }

            static long ParseParameterAsInt(NameValueCollection parameters, string name) => ParseInt(name, parameters[name]);
        }

        private string HandleUpdateDeviceSettings(string data)
        {
            StringBuilder stb = new();
            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();

            try
            {
                var jsonData = (JObject?)JsonConvert.DeserializeObject(data);

                var refId = (jsonData?["refId"]?.Value<int>()) ?? throw new ArgumentException("data is not correct");
                CheckNotNull(settingsPages);

                var tracked = jsonData?["tracked"]?.Value<bool>() ?? settingsPages.IsTracked(refId);
                var deviceSettings = new PerDeviceSettings(refId, tracked, null);

                settingsPages.AddOrUpdate(deviceSettings);
                Log.Information("Updated Device tracking {record}", deviceSettings);
            }
            catch (Exception ex)
            {
                jsonWriter.WritePropertyName("error");
                jsonWriter.WriteValue(ex.GetFullMessage());
            }
            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();
        }
    }
}