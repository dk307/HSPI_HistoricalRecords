using System;

#nullable enable

namespace Hspi
{
    internal record RecordData
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

        public long UnixTimeSeconds { get; }

        public DateTimeOffset TimeStamp => DateTimeOffset.FromUnixTimeSeconds(UnixTimeSeconds);
    }
}