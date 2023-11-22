using System;

#nullable enable

namespace Hspi
{
    internal interface IGlobalTimerAndClock
    {
        DateTimeOffset Now { get; }

        TimeSpan IntervalToRetrySqliteCollection { get; }

        TimeSpan TimeoutForBackup { get; }

        TimeSpan MaintenanceInterval { get; }
    };

    internal class GlobalTimerAndClock : IGlobalTimerAndClock
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        public TimeSpan IntervalToRetrySqliteCollection => TimeSpan.FromSeconds(15);

        public TimeSpan TimeoutForBackup => TimeSpan.FromSeconds(240);

        public TimeSpan MaintenanceInterval => TimeSpan.FromHours(1);
    }
}