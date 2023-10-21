using System;
using System.Globalization;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Serilog;

#nullable enable

namespace Hspi.DeviceData
{
    internal static class HsHomeKitDeviceFactory
    {
        public static int CreateDevice(IHsController hsController,
                                       int trackedRef,
                                       StatisticsFunction statisticsFunction,
                                       TimeSpan statisticsFunctionDuration,
                                       TimeSpan refreshInterval)
        {
            var feature = hsController.GetFeatureByRef(trackedRef);
            string deviceName = feature.Name + " " + GetStatisticsFunctionForName(statisticsFunction);

            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                                             .WithName(deviceName)
                                             .WithLocation(feature.Location)
                                             .WithLocation2(feature.Location2)
                                             .PrepareForHs();

            var newDevice = hsController.CreateDevice(newDeviceData);
            Log.Information("Created device {newDeviceName} ({newDevice}) with {function} for {name}", newDevice, trackedRef, statisticsFunction, feature.Name);

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                               .WithName(deviceName)
                                               .WithLocation(feature.Location)
                                               .WithLocation2(feature.Location2)
                                               .WithMiscFlags(EMiscFlag.StatusOnly)
                                               .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                                               .WithExtraData(new PlugExtraData())
                                               .WithDisplayType(EFeatureDisplayType.Important)
                                               .PrepareForHsDevice(newDevice);

            AddPlugExtraValue(newFeatureData, TrackedReyKey, trackedRef.ToString(CultureInfo.InvariantCulture));
            AddPlugExtraValue(newFeatureData, FunctionKey, statisticsFunction.ToString());
            AddPlugExtraValue(newFeatureData, DurationIntervalKey, statisticsFunctionDuration.ToString("c"));
            AddPlugExtraValue(newFeatureData, RefreshIntervalKey, refreshInterval.ToString("c"));

            switch (statisticsFunction)
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

        private static void AddPlugExtraValue(NewFeatureData data,
                                              string key,
                                              string value)
        {
            if (data.Feature[EProperty.PlugExtraData] is not PlugExtraData plugExtraData)
            {
                plugExtraData = new PlugExtraData();
            }
            plugExtraData.AddNamed(key, value);
            data.Feature[EProperty.PlugExtraData] = plugExtraData;
        }

        private const string DurationIntervalKey = "durationInterval";
        private const string FunctionKey = "function";
        private const string RefreshIntervalKey = "refreshInterval";
        private const string TrackedReyKey = "trackedRef";
    }
}