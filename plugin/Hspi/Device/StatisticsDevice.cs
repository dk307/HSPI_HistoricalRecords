using System;
using System.Collections.Generic;
using System.Threading;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Database;
using Hspi.Utils;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using static System.FormattableString;

#nullable enable

namespace Hspi.Device
{
    internal sealed class StatisticsDevice : IDisposable
    {
        public StatisticsDevice(IHsController hs,
                                SqliteDatabaseCollector collector,
                                int featureRefId,
                                IGlobalTimerAndClock globalTimerAndClock,
                                HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                                CancellationToken cancellationToken)
        {
            this.HS = hs;
            this.collector = collector;
            this.FeatureRefId = featureRefId;
            this.globalTimerAndClock = globalTimerAndClock;
            this.hsFeatureCachedDataProvider = hsFeatureCachedDataProvider;

            this.featureData = GetPlugExtraData<StatisticsDeviceData>(hs, featureRefId, DataKey);
            timer = new Timer(UpdateDeviceValueFromDatabase, null, 0, RefreshInterval);

            cancellationToken.Register(() =>
            {
                timer?.Dispose();
            });
        }

        public string DataFromFeatureAsJson => GetPlugExtraDataString(HS, FeatureRefId, DataKey);
        public int FeatureRefId { get; }

        private string NameForLog => HsHelper.GetNameForLog(HS, FeatureRefId);

        private int RefreshInterval
        {
            get
            {
                var refreshInterval = Math.Max(featureData.RefreshIntervalSeconds * 1000, 1000);
                var refreshInterval2 = (int)Math.Min(refreshInterval, int.MaxValue);
                return refreshInterval2;
            }
        }

        public static int CreateDevice(IHsController hsController, string name, StatisticsDeviceData data)
        {
            var feature = hsController.GetFeatureByRef(data.TrackedRef);

            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                                             .WithName(name)
                                             .WithLocation(feature.Location)
                                             .WithLocation2(feature.Location2)
                                             .PrepareForHs();

            var newDevice = hsController.CreateDevice(newDeviceData);
            Log.Information("Created device {newDeviceName} ({newDevice}) with {function} for {name}", newDevice, data.TrackedRef, data.StatisticsFunction, feature.Name);

            var plugExtraData = new PlugExtraData();
            plugExtraData.AddNamed(DataKey, JsonConvert.SerializeObject(data));

            string featureName = GetStatisticsFunctionForName(data.StatisticsFunction) + " - " +
                                                              HumanizeTimeSpan(TimeSpan.FromSeconds(data.FunctionDurationSeconds));
            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                               .WithName(featureName)
                                               .WithLocation(feature.Location)
                                               .WithLocation2(feature.Location2)
                                               .WithMiscFlags(EMiscFlag.StatusOnly)
                                               .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                                               .WithExtraData(plugExtraData)
                                               .PrepareForHsDevice(newDevice);

            switch (data.StatisticsFunction)
            {
                case StatisticsFunction.AverageStep:
                case StatisticsFunction.AverageLinear:
                case StatisticsFunction.MinValue:
                case StatisticsFunction.MaxValue:
                    newFeatureData.Feature[EProperty.AdditionalStatusData] = new List<string>(feature.AdditionalStatusData);
                    newFeatureData.Feature[EProperty.StatusGraphics] = CloneGraphics(feature.StatusGraphics);
                    break;

                default:
                    break;
            }

            return hsController.CreateFeatureForDevice(newFeatureData);

            static StatusGraphicCollection CloneGraphics(StatusGraphicCollection collection)
            {
                StatusGraphicCollection copy = new();
                foreach (var t in collection.Values)
                {
                    try
                    {
                        // some of the graphics values can be invalid and clone fails
                        copy.Add(t.Clone());
                    }
                    catch
                    {
                        //Ignore any error
                    }
                }

                return copy;
            }

            static string GetStatisticsFunctionForName(StatisticsFunction statisticsFunction)
            {
                return statisticsFunction switch
                {
                    StatisticsFunction.AverageLinear => "Average(Linear)",
                    StatisticsFunction.AverageStep => "Average(Step)",
                    StatisticsFunction.MinValue => "Minimum Value",
                    StatisticsFunction.MaxValue => "Maximum Value",
                    _ => throw new NotImplementedException(),
                };
            }
        }

        public static void EditDevice(IHsController hsController, int featureRefId, StatisticsDeviceData data)
        {
            // check same interface and is a feature
            var deviceInterface = (string)hsController.GetPropertyByRef(featureRefId, EProperty.Interface);
            var eRelationship = (ERelationship)hsController.GetPropertyByRef(featureRefId, EProperty.Relationship);
            if (deviceInterface != PlugInData.PlugInId || eRelationship != ERelationship.Feature)
            {
                throw new HsDeviceInvalidException(Invariant($"Device or feature {featureRefId} not a plugin feature"));
            }

            var plugExtraData = new PlugExtraData();
            plugExtraData.AddNamed(DataKey, JsonConvert.SerializeObject(data));
            hsController.UpdatePropertyByRef(featureRefId, EProperty.PlugExtraData, plugExtraData);

            Log.Information("Updated device {refId} with {data}", featureRefId, data);
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        public void UpdateNow() => timer.Change(0, RefreshInterval);

        private static T GetPlugExtraData<T>(IHsController hsController,
                                             int refId,
                                             string tag,
                                             params JsonConverter[] converters)
        {
            string stringData = GetPlugExtraDataString(hsController, refId, tag);
            try
            {
                var typeData = JsonConvert.DeserializeObject<T>(stringData, converters) ?? throw new HsDeviceInvalidException(Invariant($"{tag} not a valid Json value"));
                return typeData;
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found"), ex);
            }
        }

        private static string GetPlugExtraDataString(IHsController hsController, int refId, string tag)
        {
            if (hsController.GetPropertyByRef(refId, EProperty.PlugExtraData) is not PlugExtraData plugInExtra)
            {
                throw new HsDeviceInvalidException("PlugExtraData is null");
            }

            if (!plugInExtra.ContainsNamed(tag))
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found"));
            }

            var stringData = plugInExtra[tag] ?? throw new HsDeviceInvalidException(Invariant($"{tag} type not found"));
            return stringData;
        }

        private static string HumanizeTimeSpan(TimeSpan timeSpan)
        {
            List<string> parts = new();

            AddPart(timeSpan.Days, "day", parts);
            AddPart(timeSpan.Hours, "hour", parts);
            AddPart(timeSpan.Minutes, "minute", parts);
            AddPart(timeSpan.Seconds, "second", parts);

            return string.Join(" ", parts);

            static string Plural(int value)
            {
                return value > 1 ? "s" : string.Empty;
            }

            static void AddPart(int part, string partName, List<string> parts)
            {
                if (part > 0)
                {
                    parts.Add($"{part} {partName}{Plural(part)}");
                }
            }
        }

        private void UpdateDeviceValue(in double? data)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var existingValue = Convert.ToDouble(HS.GetPropertyByRef(FeatureRefId, EProperty.Value));

                Log.Write(existingValue != data ? LogEventLevel.Information : LogEventLevel.Debug,
                          "Updated value {value} for the {name}", data, NameForLog);
            }

            HS.UpdatePropertyByRef(FeatureRefId, EProperty.InvalidValue, false);

            if (data.HasValue && HasValue(data.Value))
            {
                // only this call triggers events
                if (!HS.UpdateFeatureValueByRef(FeatureRefId, data.Value))
                {
                    throw new InvalidOperationException($"Failed to update device {NameForLog}");
                }
            }
            else
            {
                HS.UpdatePropertyByRef(FeatureRefId, EProperty.InvalidValue, true);
            }

            static bool HasValue(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void UpdateDeviceValueFromDatabase(object state)
        {
            try
            {
                var max = globalTimerAndClock.Now.ToUnixTimeSeconds();
                var min = max - this.featureData.FunctionDurationSeconds;

                if (min < 0)
                {
                    throw new ArgumentException("Duration too long");
                }

                var result = this.featureData.StatisticsFunction switch
                {
                    StatisticsFunction.AverageStep or StatisticsFunction.AverageLinear => TimeAndValueQueryHelper.Average(collector,
                                                                                     featureData.TrackedRef,
                                                                                     min,
                                                                                     max,
                                                                                     this.featureData.StatisticsFunction == StatisticsFunction.AverageStep ? FillStrategy.LOCF : FillStrategy.Linear),
                    StatisticsFunction.MinValue => collector.GetMinValue(featureData.TrackedRef, min, max),
                    StatisticsFunction.MaxValue => collector.GetMaxValue(featureData.TrackedRef, min, max),
                    _ => throw new NotImplementedException(),
                };

                if (result.HasValue)
                {
                    var precision = hsFeatureCachedDataProvider.GetPrecision(featureData.TrackedRef);
                    result = Math.Round(result.Value, precision);
                }

                UpdateDeviceValue(result);
            }
            catch (Exception ex)
            {
                if (!ex.IsCancelException())
                {
                    Log.Warning("Failed to update device:{name} with {error}}", NameForLog, ExceptionHelper.GetFullMessage(ex));
                }
            }
        }

        private const string DataKey = "data";
        private readonly SqliteDatabaseCollector collector;
        private readonly StatisticsDeviceData featureData;
        private readonly IGlobalTimerAndClock globalTimerAndClock;
        private readonly IHsController HS;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly System.Threading.Timer timer;
    }
}