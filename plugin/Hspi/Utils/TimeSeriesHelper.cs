﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hspi.Database;
using Newtonsoft.Json;

#nullable enable

namespace Hspi.Utils
{
    internal sealed class TimeSeriesHelper
    {
        public TimeSeriesHelper(long minUnixTimeSeconds, long maxUnixTimeSeconds, IEnumerable<TimeAndValue> timeAndValues)
        {
            if (minUnixTimeSeconds > maxUnixTimeSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(minUnixTimeSeconds));
            }

            this.minUnixTimeSeconds = minUnixTimeSeconds;
            this.maxUnixTimeSeconds = maxUnixTimeSeconds + 1; // make max inclusive
            this.timeAndValues = timeAndValues;
        }

        public double CalculateLinearRegression()
        {
            checked
            {
                var listIterator = new TimeAndValueIterator(timeAndValues, this.maxUnixTimeSeconds);

                decimal sumOfX = 0;
                decimal sumOfY = 0;
                decimal sumOfXSq = 0;
                decimal sumOfYSq = 0;
                decimal sumCodeviates = 0;
                int count = 0;

                //list is sorted by ts, subtract minX to avoid overflow
                var minX = listIterator.IsCurrentValid ? listIterator.Current.UnixTimeSeconds : 0;

                while (listIterator.IsCurrentValid)
                {
                    count++;
                    var x = listIterator.Current.UnixTimeSeconds - minX;
                    decimal y = (decimal)listIterator.Current.DeviceValue;
                    sumCodeviates += x * y;
                    sumOfX += x;
                    sumOfY += y;
                    sumOfXSq += x * x;
                    sumOfYSq += y * y;

                    listIterator.MoveNext();
                }

                if (count <= 1)
                {
                    return 0;
                }

                decimal sCo = sumCodeviates - ((sumOfX * sumOfY) / count);
                decimal ssX = sumOfXSq - ((sumOfX * sumOfX) / count);
                decimal slope = sCo / ssX;

                return (double)slope;
            }
        }

        public IDictionary<double, long> CreateHistogram()
        {
            var listIterator = new TimeAndValueIterator(timeAndValues, this.maxUnixTimeSeconds);
            var result = new Dictionary<double, long>();

            while (listIterator.IsCurrentValid)
            {
                var intervalMin = Math.Max(this.minUnixTimeSeconds, listIterator.Current.UnixTimeSeconds);
                long duration = listIterator.FinishTimeForCurrentTimePoint - intervalMin;
                if (result.TryGetValue(listIterator.Current.DeviceValue, out var value))
                {
                    value += duration;
                    result[listIterator.Current.DeviceValue] = value;
                }
                else
                {
                    result.Add(listIterator.Current.DeviceValue, duration);
                }

                listIterator.MoveNext();
            }

            return result;
        }

        public double? Average(FillStrategy fillStrategy)
        {
            var res = ReduceSeriesWithAverage(maxUnixTimeSeconds - minUnixTimeSeconds, fillStrategy).ToList();
            if (res.Count == 0)
            {
                return null;
            }
            else
            {
                Debug.Assert(res.Count == 1);
                return res[0].DeviceValue;
            }
        }

        /// <summary>
        /// This function create a average value in the interval using spceified fill stratgey.
        /// The time stamps returned are left edge of the interval with average of value from the interval.
        /// </summary>
        /// <param name="intervalUnixTimeSeconds"></param>
        /// <param name="fillStrategy"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"> if interval <=0</exception>
        public IEnumerable<TimeAndValue> ReduceSeriesWithAverage(long intervalUnixTimeSeconds, FillStrategy fillStrategy)
        {
            if (intervalUnixTimeSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalUnixTimeSeconds));
            }

            var listIterator = new TimeAndValueIterator(timeAndValues, this.maxUnixTimeSeconds);
            var result = new SortedDictionary<long, ResultType>();

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

                            // Expanded Code
                            //var p2 = v1 + ((v2 - v1) * ((intervalMax - t1) / (t2 - t1)));
                            //var p1 = v1 + ((v2 - v1) * ((intervalMin - t1) / (t2 - t1)));
                            //var area = ((p1 + p2) / 2) * (intervalMax - intervalMin);

                            var area = ((((v2 - v1) * (intervalMin + intervalMax - 2 * t1)) / (2 * (t2 - t1))) + v1) * (intervalMax - intervalMin);

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

            static ResultType GetOrCreate(IDictionary<long, ResultType> dict, long key)

            {
                if (!dict.TryGetValue(key, out var val))
                {
                    val = new ResultType();
                    dict.Add(key, val);
                }

                return val;
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

        private readonly long maxUnixTimeSeconds;
        private readonly long minUnixTimeSeconds;
        private readonly IEnumerable<TimeAndValue> timeAndValues;
    }
}