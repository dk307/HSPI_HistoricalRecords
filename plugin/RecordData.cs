using System;
using Destructurama.Attributed;

#nullable enable

namespace Hspi
{
    public sealed record RecordData
    {
        public RecordData(long deviceRefId, in double deviceValue, string? deviceString,
                            in long unixTimeSeconds)
        {
            this.DeviceRefId = deviceRefId;
            this.DeviceValue = deviceValue;
            this.DeviceString = deviceString;
            this.UnixTimeSeconds = unixTimeSeconds;
        }

        public RecordData(long deviceRefId, in double deviceValue, string? deviceString,
                            in DateTimeOffset timeStamp)
        {
            this.DeviceRefId = deviceRefId;
            this.DeviceValue = deviceValue;
            this.DeviceString = deviceString;
            this.UnixTimeSeconds = timeStamp.ToUnixTimeSeconds();
        }

        public long DeviceRefId { get; }
        public double DeviceValue { get; }
        public string? DeviceString { get; }

        [NotLogged]
        public long UnixTimeSeconds { get; }

        [NotLogged]
        public long UnixTimeMilliSeconds => UnixTimeSeconds * 1000;

        public DateTimeOffset TimeStamp => DateTimeOffset.FromUnixTimeSeconds(UnixTimeSeconds);
    }
}