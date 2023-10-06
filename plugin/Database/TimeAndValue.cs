using System;

#nullable enable

namespace Hspi.Database
{
    public record TimeAndValue(long UnixTimeSeconds, double DeviceValue)
    {
        public long UnixTimeMilliSeconds => UnixTimeSeconds * 1000;
        public DateTimeOffset TimeStamp => DateTimeOffset.FromUnixTimeSeconds(UnixTimeSeconds);
    }
}