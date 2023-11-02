#nullable enable

using System;

namespace Hspi
{
    public sealed record PerDeviceSettings(long DeviceRefId, 
                                           bool IsTracked, 
                                           TimeSpan? RetentionPeriod,
                                           double? MinValue,
                                           double? MaxValue)
    {
    }
}