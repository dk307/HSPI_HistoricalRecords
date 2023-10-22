using System;
using System.Threading;
using System.Threading.Tasks;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Database;
using Hspi.Utils;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    public sealed record StatisticsDeviceData(int TrackedRef,
                                              StatisticsFunction StatisticsFunction,
                                              TimeSpan FunctionDuration,
                                              TimeSpan RefreshInterval);

    public sealed class StatisticsDevice : IDisposable
    {
        public StatisticsDevice(IHsController hs, SqliteDatabaseCollector collector, int refId,
                                ISystemClock systemClock, CancellationToken cancellationToken)
        {
            this.HS = hs;
            this.collector = collector;
            this.RefId = refId;
            this.systemClock = systemClock;
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            this.deviceData = GetPlugExtraData<StatisticsDeviceData>(hs, refId, DataKey);

            Utils.TaskHelper.StartAsyncWithErrorChecking($"Update RefId:{refId}", UpdateDevice, combinedToken.Token);
        }

        public int RefId { get; }

        private string NameForLog => GetNameForLog(HS, RefId);

        public static int CreateDevice(IHsController hsController, StatisticsDeviceData data)
        {
            var feature = hsController.GetFeatureByRef(data.TrackedRef);
            string deviceName = feature.Name + " Statistics";

            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                                             .WithName(deviceName)
                                             .WithLocation(feature.Location)
                                             .WithLocation2(feature.Location2)
                                             .PrepareForHs();

            var newDevice = hsController.CreateDevice(newDeviceData);
            Log.Information("Created device {newDeviceName} ({newDevice}) with {function} for {name}", newDevice, data.TrackedRef, data.StatisticsFunction, feature.Name);

            var plugExtraData = new PlugExtraData();
            plugExtraData.AddNamed(DataKey, JsonConvert.SerializeObject(data));

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                               .WithName(GetStatisticsFunctionForName(data.StatisticsFunction))
                                               .WithLocation(feature.Location)
                                               .WithLocation2(feature.Location2)
                                               .WithMiscFlags(EMiscFlag.StatusOnly)
                                               .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                                               .WithExtraData(plugExtraData)
                                               .WithDisplayType(EFeatureDisplayType.Important)
                                               .PrepareForHsDevice(newDevice);

            switch (data.StatisticsFunction)
            {
                case StatisticsFunction.AverageStep:
                case StatisticsFunction.AverageLinear:
                    newFeatureData.Feature[EProperty.AdditionalStatusData] = feature.AdditionalStatusData;
                    newFeatureData.Feature[EProperty.StatusGraphics] = feature.StatusGraphics;
                    break;

                default:
                    break;
            }

            return hsController.CreateFeatureForDevice(newFeatureData);

            static string GetStatisticsFunctionForName(StatisticsFunction statisticsFunction)
            {
                return statisticsFunction switch
                {
                    StatisticsFunction.AverageStep => "Average(Linear)",
                    StatisticsFunction.AverageLinear => "Average(Step)",
                    _ => throw new NotImplementedException(),
                };
            }
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
            if (hsController.GetPropertyByRef(refId, EProperty.PlugExtraData) is not PlugExtraData plugInExtra)
            {
                throw new HsDeviceInvalidException("PlugExtraData is null");
            }

            if (!plugInExtra.ContainsNamed(tag))
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found"));
            }

            var stringData = plugInExtra[tag] ?? throw new HsDeviceInvalidException(Invariant($"{tag} type not found"));
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

        private async Task UpdateDevice()
        {
            var shutdownToken = combinedToken.Token;
            while (!shutdownToken.IsCancellationRequested)
            {
                try
                {
                    var max = systemClock.Now;
                    var min = max - this.deviceData.FunctionDuration;
                    var result = await TimeAndValueQueryHelper.Average(collector,
                                                                       deviceData.TrackedRef,
                                                                       min.ToUnixTimeSeconds(),
                                                                       max.ToUnixTimeSeconds(),
                                                                       this.deviceData.StatisticsFunction == StatisticsFunction.AverageStep ? FillStrategy.LOCF : FillStrategy.Linear).ConfigureAwait(false);

                    UpdateDeviceValue(result);
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    Log.Warning("Failed to update device:{name} with {error}}", NameForLog, ExceptionHelper.GetFullMessage(ex));
                }

                var eventWaitTask = updateNowEvent.WaitAsync(shutdownToken);
                await Task.WhenAny(Task.Delay(deviceData.RefreshInterval, shutdownToken), eventWaitTask).ConfigureAwait(false);
            }
        }

        private void UpdateDeviceValue(in double data)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var existingValue = Convert.ToDouble(HS.GetPropertyByRef(RefId, EProperty.Value));

                Log.Write(existingValue != data ? LogEventLevel.Information : LogEventLevel.Debug,
                          "Updated value {value} for the {name}", data, NameForLog);
            }

            HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, false);

            // only this call triggers events
            if (!HS.UpdateFeatureValueByRef(RefId, data))
            {
                throw new InvalidOperationException($"Failed to update device {NameForLog}");
            }
        }

        public void Dispose()
        {
            combinedToken.Cancel();
        }

        private const string DataKey = "data";
        private readonly SqliteDatabaseCollector collector;
        private readonly CancellationTokenSource combinedToken;
        private readonly StatisticsDeviceData deviceData;
        private readonly IHsController HS;
        private readonly ISystemClock systemClock;
        private readonly AsyncAutoResetEvent updateNowEvent = new(false);
    }
}