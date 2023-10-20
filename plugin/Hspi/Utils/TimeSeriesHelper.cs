using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hspi.Database;

#nullable enable

namespace Hspi
{
    public enum FillStrategy
    {
        /// <summary>
        /// Last observation carried forward
        /// </summary>
        LOCF,

        Linear
    }

    internal sealed class TimeSeriesHelper
    {
        public TimeSeriesHelper(long minUnixTimeSeconds, long maxUnixTimeSeconds,
                                long intervalUnixTimeSeconds, IList<TimeAndValue> list) :
            this(minUnixTimeSeconds, maxUnixTimeSeconds, intervalUnixTimeSeconds, new TimeAndValueList(list))
        {
        }

        public TimeSeriesHelper(long minUnixTimeSeconds, long maxUnixTimeSeconds,
                                long intervalUnixTimeSeconds, ITimeAndValueList list)
        {
            if (intervalUnixTimeSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalUnixTimeSeconds));
            }
            if (minUnixTimeSeconds > maxUnixTimeSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(minUnixTimeSeconds));
            }
            this.minUnixTimeSeconds = minUnixTimeSeconds;
            this.maxUnixTimeSeconds = maxUnixTimeSeconds + 1; // make max inclusive
            this.intervalUnixTimeSeconds = intervalUnixTimeSeconds;
            this.list = list;
        }

        public IEnumerable<TimeAndValue> ReduceSeriesWithAverage(FillStrategy fillStrategy)
        {
            var result = new SortedDictionary<long, ResultType>();

            ListIterator listIterator = new(list, maxUnixTimeSeconds);
            for (var index = minUnixTimeSeconds; index < maxUnixTimeSeconds; index += intervalUnixTimeSeconds)
            {
                // skip initial missing values
                if (listIterator.IsCurrentValid && (listIterator.Current.UnixTimeSeconds > index) &&
                                                     (listIterator.Current.UnixTimeSeconds >= index + intervalUnixTimeSeconds))
                {
                    continue;
                }

                // consume all the points inside the interval
                // all points which start before the end of current index
                while (listIterator.IsCurrentValid &&
                       (listIterator.Current.UnixTimeSeconds < Math.Min(maxUnixTimeSeconds, index + intervalUnixTimeSeconds)))
                {
                    if (listIterator.FinishTimeForCurrentTimePoint >= index)
                    {
                        // duration is time of listMarker item between current index and its end
                        var intervalMax = Math.Min(listIterator.FinishTimeForCurrentTimePoint, index + intervalUnixTimeSeconds);
                        var intervalMin = Math.Max(index, listIterator.Current.UnixTimeSeconds);

                        if (fillStrategy == FillStrategy.Linear && listIterator.IsNextValid)
                        {
                            double v1 = listIterator.Current.DeviceValue;
                            double v2 = listIterator.Next.DeviceValue;
                            double t1 = listIterator.Current.UnixTimeSeconds;
                            double t2 = listIterator.Next.UnixTimeSeconds;

                            var p2 = v1 + ((v2 - v1) * ((intervalMax - t1) / (t2 - t1)));
                            var p1 = v1 + ((v2 - v1) * ((intervalMin - t1) / (t2 - t1)));
                            var area = ((p1 + p2) / 2) * (intervalMax - intervalMin);

                            GetOrCreate(result, index).AddArea(area, intervalMax - intervalMin);
                        }
                        else
                        {
                            GetOrCreate(result, index).AddLOCF(listIterator.Current.DeviceValue, intervalMax - intervalMin);
                        }
                    }

                    // if current timepoint goes beyond current index, move to next index
                    if (listIterator.FinishTimeForCurrentTimePoint < index + intervalUnixTimeSeconds)
                    {
                        listIterator.MoveNext();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return result.Select(x => new TimeAndValue(x.Key, x.Value.WeighedValue / x.Value.WeighedUnixSeconds));
        }

        private static ResultType GetOrCreate(IDictionary<long, ResultType> dict, long key)

        {
            if (!dict.TryGetValue(key, out var val))
            {
                val = new ResultType();
                dict.Add(key, val);
            }

            return val;
        }

        private sealed class ListIterator
        {
            public ListIterator(ITimeAndValueList list, long maxUnixTimeSeconds)
            {
                this.list = list;
                this.maxUnixTimeSeconds = maxUnixTimeSeconds;
                this.marker = 0;
            }

            public TimeAndValue Current => list[marker];

            public long FinishTimeForCurrentTimePoint
            {
                get
                {
                    if (IsNextValid)
                    {
                        return Math.Min(Next.UnixTimeSeconds, maxUnixTimeSeconds);
                    }
                    else
                    {
                        return Math.Max(maxUnixTimeSeconds, Current.UnixTimeSeconds);
                    }
                }
            }

            public bool IsCurrentValid => list.IsValidIndex(marker);
            public bool IsNextValid => list.IsValidIndex(marker + 1);
            public TimeAndValue Next => list[marker + 1];

            public void MoveNext()
            {
                marker++;
            }

            private readonly ITimeAndValueList list;
            private readonly long maxUnixTimeSeconds;
            private int marker;
        }

        private sealed class ResultType
        {
            public void AddArea(double area, long duration)
            {
                Debug.Assert(duration >= 0);
                WeighedValue += area;
                WeighedUnixSeconds += duration;
            }

            public void AddLOCF(double value, long duration)
            {
                Debug.Assert(duration >= 0);
                WeighedValue += value * duration;
                WeighedUnixSeconds += duration;
            }

            public long WeighedUnixSeconds;
            public double WeighedValue;
        }

        private readonly long intervalUnixTimeSeconds;
        private readonly ITimeAndValueList list;
        private readonly long maxUnixTimeSeconds;
        private readonly long minUnixTimeSeconds;
    }
}