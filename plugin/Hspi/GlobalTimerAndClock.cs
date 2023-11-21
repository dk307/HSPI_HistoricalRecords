using System;

#nullable enable

namespace Hspi
{
    public interface IGlobalTimerAndClock
    {
        DateTimeOffset Now { get; }

        TimeSpan IntervalToRetrySqliteCollection { get; }

        TimeSpan TimoutForBackup { get; }
    };

    public class GlobalTimerAndClock : IGlobalTimerAndClock
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        public TimeSpan IntervalToRetrySqliteCollection => TimeSpan.FromSeconds(15);

        public TimeSpan TimoutForBackup => TimeSpan.FromSeconds(240);
    }
}