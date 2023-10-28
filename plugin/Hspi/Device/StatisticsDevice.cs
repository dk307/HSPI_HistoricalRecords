using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Database;
using Hspi.Utils;
using Humanizer;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
using static System.FormattableString;

#nullable enable

namespace Hspi.Device
{
    public sealed class StatisticsDevice : IDisposable
    {
        public StatisticsDevice(IHsController hs,
                                SqliteDatabaseCollector collector,
                                int refId,
                                ISystemClock systemClock,
                                HsFeatureCachedDataProvider hsFeatureCachedDataProvider,
                                CancellationToken cancellationToken)
        {
            this.HS = hs;
            this.collector = collector;
            this.RefId = refId;
            this.systemClock = systemClock;
            this.hsFeatureCachedDataProvider = hsFeatureCachedDataProvider;
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            this.deviceData = GetPlugExtraData<StatisticsDeviceData>(hs, refId, DataKey);

            Utils.TaskHelper.StartAsyncWithErrorChecking($"Update RefId:{refId}", UpdateDevice, combinedToken.Token);
        }

        public int RefId { get; }

        private string NameForLog => GetNameForLog(HS, RefId);

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
                                                              TimeSpan.FromSeconds(data.FunctionDurationSeconds).Humanize(culture: CultureInfo.InvariantCulture);
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
                    _ => throw new NotImplementedException(),
                };
            }
        }

        public static void EditDevice(IHsController hsController, int refId, StatisticsDeviceData data)
        {
            // check same interface and is a feature
            var deviceInterface = (string)hsController.GetPropertyByRef(refId, EProperty.Interface);
            var eRelationship = (ERelationship)hsController.GetPropertyByRef(refId, EProperty.Relationship);
            if (deviceInterface != PlugInData.PlugInId || eRelationship != ERelationship.Feature)
            {
                throw new HsDeviceInvalidException(Invariant($"Device/Feature {refId} not a plugin feature"));
            }

            var plugExtraData = new PlugExtraData();
            plugExtraData.AddNamed(DataKey, JsonConvert.SerializeObject(data));
            hsController.UpdatePropertyByRef(refId, EProperty.PlugExtraData, plugExtraData);

            Log.Information("Updated device {refId} with {data}", refId, data);
        }

        public static string GetDataFromFeatureAsJson(IHsController hs, int refId) => GetPlugExtraDataString(hs, refId, DataKey);

        public void Dispose()
        {
            combinedToken.Cancel();
        }

        public void UpdateNow()
        {
            updateNowEvent.Set();
        }

        private static string GetNameForLog(IHsController hsController, int refId)
        {
            try
            {
                return hsController.GetNameByRef(refId);
            }
            catch
            {
                return Invariant($"RefId:{refId}");
            }
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

        private async Task UpdateDevice()
        {
            var shutdownToken = combinedToken.Token;
            while (!shutdownToken.IsCancellationRequested)
            {
                try
                {
                    var max = systemClock.Now.ToUnixTimeSeconds();
                    var min = max - this.deviceData.FunctionDurationSeconds;

                    if (min < 0)
                    {
                        throw new ArgumentException("Duration too long");
                    }

                    var result = await TimeAndValueQueryHelper.Average(collector,
                                                                       deviceData.TrackedRef,
                                                                       min,
                                                                       max,
                                                                       this.deviceData.StatisticsFunction == StatisticsFunction.AverageStep ? FillStrategy.LOCF : FillStrategy.Linear).ConfigureAwait(false);
                    if (result.HasValue)
                    {
                        var precision = hsFeatureCachedDataProvider.GetPrecision(deviceData.TrackedRef);
                        result = Math.Round(result.Value, precision);
                    }

                    UpdateDeviceValue(result);
                }
                catch (Exception ex) when (!ex.IsCancelException())
                {
                    Log.Warning("Failed to update device:{name} with {error}}", NameForLog, ExceptionHelper.GetFullMessage(ex));
                }

                var eventWaitTask = updateNowEvent.WaitAsync(shutdownToken);

                //validate passed intervals, must be between 1000 and int.maxValue ms
                var refreshInterval = Math.Max(deviceData.RefreshIntervalSeconds * 1000, 1000);
                var refreshInterval2 = (int)Math.Min(refreshInterval, int.MaxValue);
                await Task.WhenAny(Task.Delay(refreshInterval2, shutdownToken), eventWaitTask).ConfigureAwait(false);
            }
        }

        private void UpdateDeviceValue(in double? data)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var existingValue = Convert.ToDouble(HS.GetPropertyByRef(RefId, EProperty.Value));

                Log.Write(existingValue != data ? LogEventLevel.Information : LogEventLevel.Debug,
                          "Updated value {value} for the {name}", data, NameForLog);
            }

            HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, false);

            if (data.HasValue)
            {
                // only this call triggers events
                if (!HS.UpdateFeatureValueByRef(RefId, data.Value))
                {
                    throw new InvalidOperationException($"Failed to update device {NameForLog}");
                }
            }
            else
            {
                HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, true);
            }
        }

        private const string DataKey = "data";
        private readonly SqliteDatabaseCollector collector;
        private readonly CancellationTokenSource combinedToken;
        private readonly StatisticsDeviceData deviceData;
        private readonly IHsController HS;
        private readonly HsFeatureCachedDataProvider hsFeatureCachedDataProvider;
        private readonly ISystemClock systemClock;
        private readonly AsyncAutoResetEvent updateNowEvent = new(false);
    }
}