using System;

#nullable enable

namespace Hspi.Database
{
    public interface IDBSettings
    {
        string DBPath { get; }

        TimeSpan GetDeviceRetentionPeriod(long deviceRefId);

        long MinRecordsToKeep { get; }
    };
}