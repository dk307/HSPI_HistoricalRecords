using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Hspi.Database;

#nullable enable

namespace Hspi.Utils
{
    internal static class TimeAndValueQueryHelper
    {
        public static async Task<IEnumerable<TimeAndValue>> GetGroupedGraphValues(SqliteDatabaseCollector collector,
                                                                            int refId,
                                                                            long minUnixTimeSeconds,
                                                                            long maxUnixTimeSeconds,
                                                                            long intervalUnixTimeSeconds,
                                                                            FillStrategy fillStrategy)
        {
            IEnumerable<TimeAndValue>? result = null;
            await collector.IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, CollectAndGroup).ConfigureAwait(false);
            Debug.Assert(result != null);
            return result!;

            // this is called under db lock
            void CollectAndGroup(IEnumerable<TimeAndValue> x)
            {
                var timeSeriesHelper = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, x);
                result = timeSeriesHelper.ReduceSeriesWithAverage(intervalUnixTimeSeconds, fillStrategy);
            }
        }

        public static async Task<double> Average(SqliteDatabaseCollector collector,
                                                 int refId,
                                                 long minUnixTimeSeconds,
                                                 long maxUnixTimeSeconds,
                                                 FillStrategy fillStrategy)
        {
            double? result = null;

            await collector.IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, CollectAndGroup).ConfigureAwait(false);

            Debug.Assert(result != null);
            return result!.Value;

            // this is called under db lock
            void CollectAndGroup(IEnumerable<TimeAndValue> x)
            {
                var timeSeriesHelper = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, x);
                result = timeSeriesHelper.Average(fillStrategy);
            }
        }
    }
}