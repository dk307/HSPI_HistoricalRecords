using System.Collections.Generic;
using System.Threading.Tasks;
using Hspi.Database;

#nullable enable

namespace Hspi
{
    internal static class TimeAndValueQueryHelper
    {
        public static async Task<IList<TimeAndValue>> GetGroupedGraphValues(SqliteDatabaseCollector collector,
                                                                            int refId,
                                                                            long minUnixTimeSeconds,
                                                                            long maxUnixTimeSeconds,
                                                                            long intervalUnixTimeSeconds,
                                                                            FillStrategy fillStrategy)
        {
            IList<TimeAndValue>? result = null;
            await collector.IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, CollectAndGroup).ConfigureAwait(false);
            return result!;

            // this is called under db lock
            void CollectAndGroup(IEnumerable<TimeAndValue> x)
            {
                var timeSeriesHelper = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, intervalUnixTimeSeconds, x);
                result = timeSeriesHelper.ReduceSeriesWithAverage(fillStrategy);
            }
        }
    }
}