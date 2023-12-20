using Hspi;
using System;

namespace HSPI_HistoryTest
{
    internal record FakeGlobalClock(DateTimeOffset UtcNow, DateTimeOffset LocalNow, DayOfWeek FirstDayOfWeek) : IGlobalClock;
}