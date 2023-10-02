using System;

#nullable enable

namespace Hspi
{
    internal record RecordData
    {
        public RecordData(long deviceRefId, in double deviceValue, string? deviceString,
                            in DateTimeOffset timeStamp)
        {
            this.DeviceRefId = deviceRefId;
            this.DeviceValue = deviceValue;
            this.DeviceString = deviceString;
            this.TimeStamp = timeStamp;
        }

        public long DeviceRefId { get; }
        public double DeviceValue { get; }
        public string? DeviceString { get; }
        public DateTimeOffset TimeStamp { get; }
    }
}