using System;
using System.Globalization;

#nullable enable

namespace Hspi

{
    internal interface IGlobalClock
    {
        DateTime UtcNow { get; }

        TimeZoneInfo TimeZone { get; }

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
        public DateTime UtcNow => DateTime.UtcNow;

        public TimeZoneInfo TimeZone => TimeZoneInfo.Local;

        public DayOfWeek FirstDayOfWeek => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

        public TimeSpan IntervalToRetrySqliteCollection => TimeSpan.FromSeconds(15);

        public TimeSpan TimeoutForBackup => TimeSpan.FromSeconds(240);

        public TimeSpan MaintenanceInterval => TimeSpan.FromHours(1);
    }
}