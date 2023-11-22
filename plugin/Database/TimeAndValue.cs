using System;
using Destructurama.Attributed;

#nullable enable

namespace Hspi.Database
{
    internal sealed record TimeAndValue(long UnixTimeSeconds, in double DeviceValue)
    {
        [NotLogged]
        public long UnixTimeMilliSeconds => UnixTimeSeconds * 1000;

        [NotLogged]
        public DateTimeOffset TimeStamp => DateTimeOffset.FromUnixTimeSeconds(UnixTimeSeconds);
    }
}