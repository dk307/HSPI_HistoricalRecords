using System.Collections.Generic;
using System.Diagnostics;
using Hspi.Database;

#nullable enable

namespace Hspi.Utils
{
    internal static class TimeAndValueQueryHelper
    {
        public static IEnumerable<TimeAndValue> GetGroupedGraphValues(SqliteDatabaseCollector collector,
                                                                            int refId,
                                                                            long minUnixTimeSeconds,
                                                                            long maxUnixTimeSeconds,
                                                                            long intervalUnixTimeSeconds,
                                                                            FillStrategy fillStrategy)
        {
            IEnumerable<TimeAndValue>? result = null;
            collector.IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, CollectAndGroup);
            Debug.Assert(result != null);
            return result!;

            // this is called under db lock
            void CollectAndGroup(IEnumerable<TimeAndValue> x)
            {
                var timeSeriesHelper = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, x);
                result = timeSeriesHelper.ReduceSeriesWithAverage(intervalUnixTimeSeconds, fillStrategy);
            }
        }

        public static double? Average(SqliteDatabaseCollector collector,
                                                 int refId,
                                                 long minUnixTimeSeconds,
                                                 long maxUnixTimeSeconds,
                                                 FillStrategy fillStrategy)
        {
            double? result = null;

            collector.IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, CollectAndGroup);
            return result;

            // this is called under db lock
            void CollectAndGroup(IEnumerable<TimeAndValue> x)
            {
                var timeSeriesHelper = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, x);
                result = timeSeriesHelper.Average(fillStrategy);
            }
        }
    }
}