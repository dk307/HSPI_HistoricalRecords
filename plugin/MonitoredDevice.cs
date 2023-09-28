#nullable enable

using System;

namespace Hspi
{
    [Flags]
    public enum TrackedType
    {
        Value = 0x01,
        String = 0x02,
        Both = Value | String,
    }

    public sealed record MonitoredDevice
    {
        public MonitoredDevice(int deviceRefId,
                               double? maxValidValue = null, double? minValidValue = null,
                               TrackedType? trackedType = null)
        {
            DeviceRefId = deviceRefId;
            MaxValidValue = maxValidValue;
            MinValidValue = minValidValue;
            TrackedType = trackedType ?? TrackedType.Both;
        }

        public int DeviceRefId { get; }
        public double? MaxValidValue { get; }
        public double? MinValidValue { get; }
        public TrackedType TrackedType { get; }
    }
}