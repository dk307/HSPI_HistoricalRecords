﻿using System;
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
using Humanizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx.Synchronous;
using Serilog;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public IList<string> GetAllowedDisplays(object? refIdString)
        {
            var refId = TypeConverter.TryGetFromObject<int>(refIdString) ?? throw new ArgumentException(null, nameof(refIdString));

            var feature = new HsFeatureData(HomeSeerSystem, refId);

            List<string> displays = new()
            {
                "table"
            };

            if (ShouldShowChart(feature))
            {
                displays.Add("chart");
            }

            return displays;

            static bool ShouldShowChart(HsFeatureData feature)
            {
                if (IsOnlyOnOffFeature(feature) && !HasAnyRangeGraphics(feature))
                {
                    return false;
                }

                return true;
            }
            static bool IsOnlyOnOffFeature(HsFeatureData feature)
            {
                return feature.StatusControls.TrueForAll(x => x.ControlUse is EControlUse.On or EControlUse.Off);
            }

            static bool HasAnyRangeGraphics(HsFeatureData feature)
            {
                return feature.StatusGraphics.Exists(x => x.IsRange);
            }
        }

        public List<object?> GetDeviceStatsForPage(object? refIdString)
        {
            var refId = TypeConverter.TryGetFromObject<int>(refIdString) ?? throw new ArgumentException(null, nameof(refIdString));
            var result = new List<object?>();

            result.AddRange(GetEarliestAndOldestRecordTotalSeconds(refId).Select(x => (object)x));
            result.Add(IsFeatureTracked(refId));
            result.Add(GetFeaturePrecision(refId));
            result.Add(GetFeatureUnit(refId) ?? string.Empty);

            return result;
        }

        public IList<int> GetFeatureRefIdsForDevice(object? refIdString)
        {
            var refId = TypeConverter.TryGetFromObject<int>(refIdString) ?? throw new ArgumentException(null, nameof(refIdString));
            var hashSet = (HashSet<int>)HomeSeerSystem.GetPropertyByRef(refId, EProperty.AssociatedDevices);
            hashSet.Add(refId);
            return hashSet.ToList();
        }

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            Log.Debug("PostBackProc for {page} for {param}", page, data);
            try
            {
                return page switch
                {
                    "historyrecords" => HandleHistoryRecords(data).WaitAndUnwrapException(ShutdownCancellationToken),
                    "graphrecords" => HandleGraphRecords(data).WaitAndUnwrapException(ShutdownCancellationToken),
                    "updatedevicesettings" => HandleUpdateDeviceSettings(data),
                    "devicecreate" => HandleDeviceCreate(data),
                    "deviceedit" => HandleDeviceEdit(data),
                    _ => base.PostBackProc(page, data, user, userRights),
                };
            }
            catch (Exception ex)
            {
                Log.Error("Error in Page {page} for {param} with {error}", page, data, ex.GetFullMessage());
                return WriteExceptionResultAsJson(ex);
            }
        }

        internal IList<long> GetEarliestAndOldestRecordTotalSeconds(int refId)
        {
            var data = Collector.GetEarliestAndOldestRecordTimeDate(refId).WaitAndUnwrapException(ShutdownCancellationToken);

            var now = CreateClock().Now;

            return new List<long>() {
                (long)Math.Round((now - data.Item1).TotalSeconds),
                (long)Math.Round((now - data.Item2).TotalSeconds)
                };
        }

        internal int GetFeaturePrecision(int refId)
        {
            CheckNotNull(featureCachedDataProvider);
            return featureCachedDataProvider.GetPrecision(refId);
        }

        internal string? GetFeatureUnit(int refId)
        {
            CheckNotNull(featureCachedDataProvider);
            return featureCachedDataProvider.GetUnit(refId);
        }

        internal long GetTotalRecords(int refId)
        {
            var count = Collector.GetRecordsCount(refId, 0, long.MaxValue).WaitAndUnwrapException(ShutdownCancellationToken);
            return count;
        }

        internal long DeleteAllRecords(int refId)
        {
            var count = Collector.DeleteAllRecordsForRef(refId).WaitAndUnwrapException(ShutdownCancellationToken);
            return count;
        }

        internal bool IsFeatureTracked(int refId)
        {
            CheckNotNull(settingsPages);
            CheckNotNull(featureCachedDataProvider);
            return settingsPages.IsTracked(refId) &&
                     featureCachedDataProvider.IsMonitorableTypeFeature(refId);
        }

        protected virtual ISystemClock CreateClock() => new SystemClock();

        private static TimeSpan GetDefaultGroupInterval(TimeSpan duration)
        {
            // aim for 256 points on graph
            return TimeSpan.FromSeconds(duration.TotalSeconds / MaxGraphPoints);
        }

        private static T GetJsonValue<T>(JObject? json, string tokenStr)
        {
            try
            {
                var token = (json?.SelectToken(tokenStr)) ?? throw new ArgumentException(tokenStr + " is not correct");
                return token.Value<T?>() ?? throw new ArgumentException(tokenStr + " is not correct");
            }
            catch (Exception ex)
            {
                throw new ArgumentException(tokenStr + " is not correct", ex);
            }
        }

        private static string WriteExceptionResultAsJson(Exception ex)
        {
            StringBuilder stb = new();
            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("error");
            jsonWriter.WriteValue(ex.GetFullMessage());
            jsonWriter.WriteEndObject();
            jsonWriter.Close();
            return stb.ToString();
        }

        private string CreateTrackedDeviceConfigPage(int devOrFeatRef, string iFrameName)
        {
            DetermineDeviceAndFeatureRefIds(devOrFeatRef, out var parentRefId, out var featureRefId);

            StringBuilder stb = new();
            stb.Append("<script>$('#save_device_config').hide();</script>");

            string iFrameUrl = Invariant($"{CreatePlugInUrl(iFrameName)}?ref={parentRefId}&feature={featureRefId}");

            // iframeSizer.min.js
            stb.Append($"<script src=\"{CreatePlugInUrl("iframeResizer.min.js")}\"></script>");
            stb.Append(@"<style>iframe{width: 1px;min-width: 100%;min-height: 40rem; border: 0px;}</style>");
            string id = "historicalrecordsiframeid";
            stb.Append(Invariant($"<iframe id=\"{id}\" src=\"{iFrameUrl}\"></iframe>"));
            stb.Append(Invariant($"<script>iFrameResize({{log: true, inPageLinks: true }}, '#{id}');</script>"));

            LabelView labelView = new("id", stb.ToString())
            {
                LabelType = HomeSeer.Jui.Types.ELabelType.Preformatted
            };

            var page = PageFactory.CreateDeviceConfigPage(Id, "Device");
            page.Page.AddView(labelView);
            return page.Page.ToJsonString();

            string CreatePlugInUrl(string fileName)
            {
                return "/" + Id + "/" + fileName;
            }
        }

        private void DetermineDeviceAndFeatureRefIds(int devOrFeatRef, out int parentRefId, out int featureRefId)
        {
            bool isDevice = HomeSeerSystem.IsRefDevice(devOrFeatRef);
            if (isDevice)
            {
                parentRefId = devOrFeatRef;
                featureRefId = devOrFeatRef;
            }
            else
            {
                parentRefId = ((HashSet<int>)HomeSeerSystem.GetPropertyByRef(devOrFeatRef, EProperty.AssociatedDevices)).First();
                featureRefId = devOrFeatRef;
            }
        }

        private async Task<string> HandleGraphRecords(string data)
        {
            var jsonData = (JObject?)JsonConvert.DeserializeObject(data);

            var refId = GetJsonValue<int>(jsonData, "refId");
            var min = GetJsonValue<long>(jsonData, "min");
            var max = GetJsonValue<long>(jsonData, "max");

            if (max < min)
            {
                throw new ArgumentException("max < min");
            }

            var fillStrategy = GetFillStrategy(jsonData);

            long groupBySeconds = (long)Math.Round(GetDefaultGroupInterval(TimeSpan.FromMilliseconds(max - min)).TotalSeconds);
            bool shouldGroup = groupBySeconds >= 5;

            var queryData = shouldGroup ?
                            await TimeAndValueQueryHelper.GetGroupedGraphValues(Collector, refId, min / 1000, max / 1000, groupBySeconds, fillStrategy).ConfigureAwait(false) :
                            await Collector.GetGraphValues(refId, min / 1000, max / 1000).ConfigureAwait(false);

            CheckNotNull(featureCachedDataProvider);
            StringBuilder stb = new();
            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("result");
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("groupedbyseconds");
            jsonWriter.WriteValue(shouldGroup ? groupBySeconds : 0);
            jsonWriter.WritePropertyName("data");
            jsonWriter.WriteStartArray();

            foreach (var row in queryData)
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

            jsonWriter.WriteEndObject();
            jsonWriter.Close();

            return stb.ToString();

            static FillStrategy GetFillStrategy(JObject? jsonData)
            {
                try
                {
                    var fillStrategyStr = GetJsonValue<string>(jsonData, "fill");
                    return fillStrategyStr.DehumanizeTo<FillStrategy>();
                }
                catch (NoMatchFoundException ex)
                {
                    throw new ArgumentException("fill is not correct", ex);
                }
            }
        }

        private async Task<string> HandleHistoryRecords(string data)
        {
            Log.Debug("HandleHistoryRecords {data}", data);

            StringBuilder stb = new();
            var parameters = HttpUtility.ParseQueryString(data);

            var refId = ParseParameterAsInt(parameters, "refId");
            var start = ParseParameterAsInt(parameters, "start");
            var length = ParseParameterAsInt(parameters, "length");
            var sortOrder = CalculateSortOrder(parameters["order[0][column]"], parameters["order[0][dir]"]);

            long totalResultsCount;

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

            using var stringWriter = new StringWriter(stb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.Formatting = Formatting.Indented;
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("draw");
            jsonWriter.WriteValue(parameters["draw"]);

            jsonWriter.WritePropertyName("recordsTotal");
            jsonWriter.WriteValue(totalResultsCount);

            jsonWriter.WritePropertyName("recordsFiltered");
            jsonWriter.WriteValue(totalResultsCount);

            jsonWriter.WritePropertyName("data");
            jsonWriter.WriteStartArray();

            CheckNotNull(featureCachedDataProvider);
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

            static long ParseParameterAsInt(NameValueCollection parameters, string name)
            {
                return TypeConverter.TryGetFromObject<long>(parameters[name]) ?? throw new ArgumentException(name + " is invalid");
            }
        }

        private string HandleUpdateDeviceSettings(string data)
        {
            var jsonData = (JObject?)JsonConvert.DeserializeObject(data);

            var refId = GetJsonValue<int>(jsonData, "refId");
            var tracked = GetJsonValue<bool>(jsonData, "tracked");

            var deviceSettings = new PerDeviceSettings(refId, tracked, null);
            CheckNotNull(settingsPages);
            settingsPages.AddOrUpdate(deviceSettings);
            Log.Information("Updated Device tracking {record}", deviceSettings);
            return "{}";
        }

        public const int MaxGraphPoints = 256;
    }
}