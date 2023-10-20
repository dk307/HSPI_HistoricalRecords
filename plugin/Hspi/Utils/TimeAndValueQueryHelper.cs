using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Hspi.Database;

#nullable enable

namespace Hspi
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

        //public static async Task<double> Average(SqliteDatabaseCollector collector,
        //                                         int refId,
        //                                         long minUnixTimeSeconds,
        //                                         long maxUnixTimeSeconds,
        //                                         FillStrategy fillStrategy)
        //{
        //    IList<TimeAndValue>? result = null;

        //    await collector.IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, CollectAndGroup).ConfigureAwait(false);

        //    Debug.Assert(result != null);
        //    Debug.Assert(result!.Count == 1);
        //    return result[0].DeviceValue;

        //    // this is called under db lock
        //    void CollectAndGroup(IEnumerable<TimeAndValue> x)
        //    {
        //        var timeSeriesHelper = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, , x);
        //        result = timeSeriesHelper.ReduceSeriesWithAverage(fillStrategy, ).ToList();
        //    }
        //}
    }
}