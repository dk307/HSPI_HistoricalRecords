using System;
using System.Globalization;

#nullable enable

namespace Hspi

{
    internal interface IGlobalClock
    {
        DateTimeOffset LocalNow { get; }

        DateTimeOffset UtcNow { get; }

        DayOfWeek FirstDayOfWeek { get; }
    };

    internal interface IGlobalTimerAndClock : IGlobalClock
    {
        TimeSpan IntervalToRetrySqliteCollection { get; }

        TimeSpan TimeoutForBackup { get; }

        TimeSpan MaintenanceInterval { get; }
    };

    internal class GlobalTimerAndClock : IGlobalTimerAndClock, IGlobalClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset LocalNow => DateTimeOffset.Now;

        public DayOfWeek FirstDayOfWeek => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

        public TimeSpan IntervalToRetrySqliteCollection => TimeSpan.FromSeconds(15);

        public TimeSpan TimeoutForBackup => TimeSpan.FromSeconds(240);

        public TimeSpan MaintenanceInterval => TimeSpan.FromHours(1);
    }
}