using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hspi.Database;

#nullable enable

namespace Hspi
{
    internal class TimeAndValueList : ITimeAndValueList
    {
        public TimeAndValueList(IList<TimeAndValue> list)
        {
            this.list = list;
        }

        public TimeAndValue this[int index] => list[index];

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < list.Count;
        }

        private readonly IList<TimeAndValue> list;
    }

    internal class TimeSeriesHelper
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

        public IEnumerable<TimeAndValue> ReduceSeriesWithAverageAndPreviousFill()
        {
            var result = new SortedDictionary<long, ResultType>();

            int listMarker = 0;
            int? previousValidMarker = null;
            for (var index = minUnixTimeSeconds; index < maxUnixTimeSeconds; index += intervalUnixTimeSeconds)
            {
                if (list.IsValidIndex(listMarker) && (list[listMarker].UnixTimeSeconds > index) &&
                                                     (list[listMarker].UnixTimeSeconds >= index + intervalUnixTimeSeconds))
                {
                    AddPreviousMarkerValues(result, previousValidMarker, index);
                    continue;
                }

                // consume all the points inside the interval
                // all points which start before the end of current index
                bool anyAdded = false;
                while (list.IsValidIndex(listMarker) &&
                       (list[listMarker].UnixTimeSeconds < Math.Min(maxUnixTimeSeconds, index + intervalUnixTimeSeconds)))
                {
                    // duration is time of listMarker item between current index and its end
                    var duration = GetDurationInIndex(listMarker, index);
                    GetOrCreate(result, index).Add(list[listMarker].DeviceValue, duration);
                    previousValidMarker = listMarker;
                    anyAdded = true;

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

                if (!anyAdded && !list.IsValidIndex(listMarker))
                {
                    AddPreviousMarkerValues(result, previousValidMarker, index);
                }
            }

            return result.Select(x => new TimeAndValue(x.Key, x.Value.WeighedValue / x.Value.WeighedUnixSeconds));

            long GetDurationInIndex(int marker, long indexV)
            {
                var duration = Math.Min(GetFinishTimeForTimePoint(marker), indexV + intervalUnixTimeSeconds)
                                 - Math.Max(indexV, list[marker].UnixTimeSeconds);
                return duration;
            }

            void AddPreviousMarkerValues(IDictionary<long, ResultType> result,
                                         int? previousValidMarkerV, long indexV)
            {
                if (previousValidMarkerV != null)
                {
                    var duration = GetDurationInIndex(previousValidMarkerV.Value, indexV);
                    GetOrCreate(result, indexV).Add(list[previousValidMarkerV.Value].DeviceValue, duration);
                }
            }
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

        private class ResultType
        {
            public void Add(double value, long duration)
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