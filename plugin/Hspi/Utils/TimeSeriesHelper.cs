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

            int listMarker = 0;
            for (var index = minUnixTimeSeconds; index < maxUnixTimeSeconds; index += intervalUnixTimeSeconds)
            {
                // skip initial missing values
                if (list.IsValidIndex(listMarker) && (list[listMarker].UnixTimeSeconds > index) &&
                                                     (list[listMarker].UnixTimeSeconds >= index + intervalUnixTimeSeconds))
                {
                    continue;
                }

                // consume all the points inside the interval
                // all points which start before the end of current index
                while (list.IsValidIndex(listMarker) &&
                       (list[listMarker].UnixTimeSeconds < Math.Min(maxUnixTimeSeconds, index + intervalUnixTimeSeconds)))
                {
                    if (GetFinishTimeForTimePoint(listMarker) >= index)
                    {
                        // duration is time of listMarker item between current index and its end
                        var intervalMax = Math.Min(GetFinishTimeForTimePoint(listMarker), index + intervalUnixTimeSeconds);
                        var intervalMin = Math.Max(index, list[listMarker].UnixTimeSeconds);

                        if (fillStrategy == FillStrategy.Linear && list.IsValidIndex(listMarker + 1))
                        {
                            double v1 = list[listMarker].DeviceValue;
                            double v2 = list[listMarker + 1].DeviceValue;
                            double t1 = list[listMarker].UnixTimeSeconds;
                            double t2 = list[listMarker + 1].UnixTimeSeconds;

                            var p2 = v1 + ((v2 - v1) * ((intervalMax - t1) / (t2 - t1)));
                            var p1 = v1 + ((v2 - v1) * ((intervalMin - t1) / (t2 - t1)));
                            var area = ((p1 + p2) / 2) * (intervalMax - intervalMin);

                            GetOrCreate(result, index).AddArea(area, intervalMax - intervalMin);
                        }
                        else
                        {
                            GetOrCreate(result, index).AddLOCF(list[listMarker].DeviceValue, intervalMax - intervalMin);
                        }
                    }

                    // if current timepoint goes beyond current index, move to next index
                    if (GetFinishTimeForTimePoint(listMarker) < index + intervalUnixTimeSeconds)
                    {
                        listMarker++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return result.Select(x => new TimeAndValue(x.Key, x.Value.WeighedValue / x.Value.WeighedUnixSeconds));
        }

        /// <summary>
        /// LOCF
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TimeAndValue> ReduceSeriesWithAverageAndPreviousFill() => ReduceSeriesWithAverage(FillStrategy.LOCF);

        private static ResultType GetOrCreate(IDictionary<long, ResultType> dict, long key)

        {
            if (!dict.TryGetValue(key, out var val))
            {
                val = new ResultType();
                dict.Add(key, val);
            }

            return val;
        }

        private long GetFinishTimeForTimePoint(int index)
        {
            if (list.IsValidIndex(index + 1))
            {
                return Math.Min(list[index + 1].UnixTimeSeconds, maxUnixTimeSeconds);
            }
            else
            {
                return Math.Max(maxUnixTimeSeconds, list[index].UnixTimeSeconds);
            }
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