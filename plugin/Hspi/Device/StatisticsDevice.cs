using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                                IGlobalClock globalClock,
                                HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                                CancellationToken cancellationToken)
        {
            this.HS = hs;
            this.collector = collector;
            this.FeatureRefId = featureRefId;
            this.globalClock = globalClock;
            this.hsFeatureCachedDataProvider = hsFeatureCachedDataProvider;
            this.featureData = GetPlugExtraData<StatisticsDeviceData>(hs, featureRefId, DataKey);
            this.period = featureData.StatisticsFunctionDuration.DerviedPeriod;
            this.timer = new StatisticsDeviceTimer(globalClock, this.period,
                                                   RefreshIntervalMilliseconds,
                                                   UpdateDeviceValueFromDatabase,
                                                   cancellationToken);
        }

        public string DataFromFeatureAsJson => GetPlugExtraDataString(HS, FeatureRefId, DataKey);

        public int FeatureRefId { get; init; }

        public int TrackedRefId => this.featureData.TrackedRef;

        private string NameForLog => HsHelper.GetNameForLog(HS, FeatureRefId);

        private long RefreshIntervalMilliseconds => Math.Max(featureData.RefreshIntervalSeconds * 1000, 1000);

        public static int Create(HsFeatureCachedDataProvider hsFeatureCachedDataProvider, int parentRefId, StatisticsDeviceData data)
        {
            return CreateImpl(hsFeatureCachedDataProvider, parentRefId, null, data);
        }

        public static int Create(HsFeatureCachedDataProvider hsFeatureCachedDataProvider, string deviceName, StatisticsDeviceData data)
        {
            return CreateImpl(hsFeatureCachedDataProvider, null, deviceName, data);
        }

        public static void EditDevice(IHsController hsController, int featureRefId, StatisticsDeviceData data)
        {
            // check same interface and is a trackedFeature
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

        public void UpdateNow() => timer.UpdateNow();

        private static int CreateImpl(HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                                      int? parentRefId,
                                      string? newDeviceName,
                                      StatisticsDeviceData data)
        {
            var trackedFeature = hsFeatureCachedDataProvider.HomeSeerSystem.GetFeatureByRef(data.TrackedRef);

            int featureParentRefId;
            if (!parentRefId.HasValue)
            {
                var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                                                 .WithName(newDeviceName)
                                                 .WithLocation(trackedFeature.Location)
                                                 .WithLocation2(trackedFeature.Location2)
                                                 .PrepareForHs();

                featureParentRefId = hsFeatureCachedDataProvider.HomeSeerSystem.CreateDevice(newDeviceData);
                Log.Information("Created device {name}", HsHelper.GetNameForLog(hsFeatureCachedDataProvider.HomeSeerSystem, featureParentRefId));
            }
            else
            {
                featureParentRefId = parentRefId.Value;
            }

            var plugExtraData = new PlugExtraData();
            plugExtraData.AddNamed(DataKey, JsonConvert.SerializeObject(data));

            string? suffix = data.StatisticsFunctionDuration.Humanize();
            string featureName = GetStatisticsFunctionForName(data.StatisticsFunction) +
                                    (string.IsNullOrWhiteSpace(suffix) ? string.Empty : (" - " + suffix));
            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                               .WithName(featureName)
                                               .WithLocation(trackedFeature.Location)
                                               .WithLocation2(trackedFeature.Location2)
                                               .WithMiscFlags(EMiscFlag.StatusOnly)
                                               .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                                               .WithExtraData(plugExtraData)
                                               .PrepareForHsDevice(featureParentRefId);

            switch (data.StatisticsFunction)
            {
                case StatisticsFunction.AverageStep:
                case StatisticsFunction.AverageLinear:
                case StatisticsFunction.MinimumValue:
                case StatisticsFunction.MaximumValue:
                case StatisticsFunction.DistanceBetweenMinAndMax:
                case StatisticsFunction.Difference:
                    newFeatureData.Feature[EProperty.AdditionalStatusData] = new List<string>(trackedFeature.AdditionalStatusData);
                    newFeatureData.Feature[EProperty.StatusGraphics] = CloneGraphics(trackedFeature.Ref, hsFeatureCachedDataProvider,
                                                                                     trackedFeature.StatusGraphics);
                    break;

                case StatisticsFunction.RecordsCount:
                case StatisticsFunction.ValueChangedCount:
                    {
                        var graphic = new StatusGraphic(GetImagePath("count"), int.MinValue, int.MaxValue);
                        graphic.TargetRange.DecimalPlaces = 0;

                        var graphics = new StatusGraphicCollection();
                        graphics.Add(graphic);
                        newFeatureData.Feature[EProperty.StatusGraphics] = graphics;
                    }

                    break;

                case StatisticsFunction.LinearRegression:
                    {
                        var trackedFeatureUnit = hsFeatureCachedDataProvider.GetUnit(data.TrackedRef);
                        string unitForGraphic = string.IsNullOrWhiteSpace(trackedFeatureUnit) ? "per minute" :
                                                                                                 Invariant($"{trackedFeatureUnit} per minute");
                        newFeatureData.Feature[EProperty.AdditionalStatusData] = new List<string?>() { unitForGraphic };

                        var graphic = new StatusGraphic(GetImagePath("count"), int.MinValue, int.MaxValue);
                        graphic.TargetRange.DecimalPlaces = 5;
                        graphic.HasAdditionalData = true;
                        graphic.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);

                        var graphics = new StatusGraphicCollection();
                        graphics.Add(graphic);
                        newFeatureData.Feature[EProperty.StatusGraphics] = graphics;
                    }

                    break;

                default:
                    break;
            }

            int featureId = hsFeatureCachedDataProvider.HomeSeerSystem.CreateFeatureForDevice(newFeatureData);
            Log.Information("Created {featureName} for  tracking {trackedName} for {name}",
                            HsHelper.GetNameForLog(hsFeatureCachedDataProvider.HomeSeerSystem, featureId),
                            HsHelper.GetNameForLog(hsFeatureCachedDataProvider.HomeSeerSystem, data.TrackedRef),
                            data.StatisticsFunction.ToString());

            return featureId;

            static StatusGraphicCollection CloneGraphics(int refId,
                                                         HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                                                         StatusGraphicCollection collection)
            {
                StatusGraphicCollection copy = new();
                foreach (var original in collection.Values)
                {
                    try
                    {
                        // some of the graphics values can be invalid and clone fails
                        StatusGraphic statusGraphic = original.Clone();

                        // for HS3 devices, decimal places are not correct
                        if (statusGraphic.IsRange)
                        {
                            statusGraphic.TargetRange.DecimalPlaces = Math.Max(hsFeatureCachedDataProvider.GetPrecision(refId),
                                                                               statusGraphic.TargetRange.DecimalPlaces);
                        }

                        copy.Add(statusGraphic);
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
                    StatisticsFunction.MinimumValue => "Minimum Value",
                    StatisticsFunction.MaximumValue => "Maximum Value",
                    StatisticsFunction.DistanceBetweenMinAndMax => "Distance Min-Max Value",
                    StatisticsFunction.RecordsCount => "Count",
                    StatisticsFunction.ValueChangedCount => "Value Changed Count",
                    StatisticsFunction.LinearRegression => "Slope",
                    StatisticsFunction.Difference => "Difference",
                    _ => throw new NotImplementedException(),
                };
            }
        }

        private static string GetImagePath(string iconFileName)
        {
            return Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", iconFileName), "png");
        }

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

        private void UpdateDeviceValue(in double? data)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var existingValue = Convert.ToDouble(HS.GetPropertyByRef(FeatureRefId, EProperty.Value));

                Log.Write(existingValue != data ? LogEventLevel.Information : LogEventLevel.Debug,
                          "Updated value {value} for the {name}", data, NameForLog);
            }

            if (data.HasValue && HasValue(data.Value))
            {
                HS.UpdatePropertyByRef(FeatureRefId, EProperty.InvalidValue, false);

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

        private void UpdateDeviceValueFromDatabase()
        {
            try
            {
                var minMaxTime = this.period.CalculateMinMaxSeconds(globalClock);

                if (minMaxTime.IsValid)
                {
                    if (minMaxTime.Minimum < 0)
                    {
                        throw new ArgumentException("Duration too long");
                    }

                    var result = this.featureData.StatisticsFunction switch
                    {
                        StatisticsFunction.AverageStep or StatisticsFunction.AverageLinear => TimeAndValueQueryHelper.Average(collector,
                                                                                         featureData.TrackedRef,
                                                                                         minMaxTime.Minimum,
                                                                                         minMaxTime.Maximum,
                                                                                         this.featureData.StatisticsFunction == StatisticsFunction.AverageStep ? FillStrategy.LOCF : FillStrategy.Linear),
                        StatisticsFunction.MinimumValue => collector.GetMinValue(featureData.TrackedRef, minMaxTime.Minimum, minMaxTime.Maximum),
                        StatisticsFunction.MaximumValue => collector.GetMaxValue(featureData.TrackedRef, minMaxTime.Minimum, minMaxTime.Maximum),
                        StatisticsFunction.DistanceBetweenMinAndMax => collector.GetDistanceMinMaxValue(featureData.TrackedRef, minMaxTime.Minimum, minMaxTime.Maximum),
                        StatisticsFunction.RecordsCount => collector.GetRecordsCount(featureData.TrackedRef, minMaxTime.Minimum, minMaxTime.Maximum),
                        StatisticsFunction.ValueChangedCount => collector.GetChangedValuesCount(featureData.TrackedRef, minMaxTime.Minimum, minMaxTime.Maximum),
                        StatisticsFunction.LinearRegression => 60 * TimeAndValueQueryHelper.LinearRegression(collector, featureData.TrackedRef, minMaxTime.Minimum, minMaxTime.Maximum),
                        StatisticsFunction.Difference => collector.GetDifferenceFromValuesAt(featureData.TrackedRef, minMaxTime.Minimum, minMaxTime.Maximum),
                        _ => throw new NotImplementedException(),
                    };

                    if (result.HasValue)
                    {
                        var precision = hsFeatureCachedDataProvider.GetPrecision(featureData.TrackedRef);
                        result = Math.Round(result.Value, precision);
                    }

                    UpdateDeviceValue(result);
                }
                else
                {
                    Debug.Assert(minMaxTime.Minimum <= minMaxTime.Maximum);
                }
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
        private readonly IGlobalClock globalClock;
        private readonly IHsController HS;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly Period period;
        private readonly StatisticsDeviceTimer timer;
    }
}