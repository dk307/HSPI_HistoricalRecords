using System;
using System.Threading;

#nullable enable

namespace Hspi.Device
{
    internal sealed class StatisticsDeviceTimer : IDisposable
    {
        public StatisticsDeviceTimer(IGlobalClock globalClock,
                                     Period period,
                                     long refreshIntevalMilliseconds,
                                     Action callback,
                                     CancellationToken cancellationToken)
        {
            this.globalClock = globalClock;
            this.period = period;
            this.refreshIntervalMilliseconds = refreshIntevalMilliseconds;
            this.timer = new Timer(ExecCallback, null, 0, Timeout.Infinite);

            cancellationToken.Register(() =>
            {
                timer?.Dispose();
            });

            void ExecCallback(object _)
            {
                callback();
                this.timer?.Change(CalculateNextTimerFireInterval(), System.Threading.Timeout.Infinite);
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        public void UpdateNow() => timer.Change(0, refreshIntervalMilliseconds);

        private static TimeSpan? GetNextTimerPeriod(IGlobalClock globalClock, InstantType? instantType)
        {
            if (instantType is null or InstantType.Now)
            {
                return null;
            }

            var localNow = globalClock.LocalNow;

            DateTimeOffset nextCliff = instantType switch
            {
                InstantType.StartOfHour => localNow.StartOfHour().AddHours(1),
                InstantType.StartOfDay => localNow.StartOfDay().AddDays(1),
                InstantType.StartOfWeek => localNow.StartOfWeek(globalClock.FirstDayOfWeek).AddDays(7),
                InstantType.StartOfMonth => localNow.StartOfMonth().AddMonths(1),
                InstantType.StartOfYear => localNow.StartOfYear().AddYears(1),
                _ => throw new NotImplementedException(),
            };

            return nextCliff - localNow;
        }

        private long CalculateNextTimerFireInterval()
        {
            var start = GetNextTimerPeriod(globalClock, this.period.Start?.Type);
            var end = GetNextTimerPeriod(globalClock, this.period.End?.Type);

            var result = refreshIntervalMilliseconds;
            if (start is not null)
            {
                result = Math.Min(result, (long)start.Value.TotalMilliseconds);
            }

            if (end is not null)
            {
                result = Math.Min(result, (long)end.Value.TotalMilliseconds);
            }

            return result;
        }

        private readonly IGlobalClock globalClock;
        private readonly Period period;
        private readonly long refreshIntervalMilliseconds;
        private readonly System.Threading.Timer timer;
    }
}