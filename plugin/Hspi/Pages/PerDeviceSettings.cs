#nullable enable

using System;

namespace Hspi
{
    internal sealed record PerDeviceSettings(long DeviceRefId,
                                           bool IsTracked,
                                           TimeSpan? RetentionPeriod,
                                           double? MinValue,
                                           double? MaxValue)
    {
    }
}