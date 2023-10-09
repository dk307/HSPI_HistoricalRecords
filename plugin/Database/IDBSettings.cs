using System;

#nullable enable

namespace Hspi.Database
{
    internal interface IDBSettings
    {
        string DBPath { get; }
        TimeSpan GetDeviceRetentionPeriod(long deviceRefId);

        long MinRecordsToKeep { get; }
    };
}