using System;
using Destructurama.Attributed;

#nullable enable

namespace Hspi.Database
{
    internal record struct TimeAndValue(long UnixTimeSeconds, in double DeviceValue)
    {
        [NotLogged]
        public readonly long UnixTimeMilliSeconds => UnixTimeSeconds * 1000;

        [NotLogged]
        public readonly DateTimeOffset TimeStamp => DateTimeOffset.FromUnixTimeSeconds(UnixTimeSeconds);
    }
}