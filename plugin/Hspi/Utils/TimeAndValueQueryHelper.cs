﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Hspi.Database;

#nullable enable

namespace Hspi
{
    internal static class TimeAndValueQueryHelper
    {
        public static async Task<IEnumerable<TimeAndValue>?> GetGroupedGraphValues(SqliteDatabaseCollector collector,
                                                                                  int refId,
                                                                                  long minUnixTimeSeconds,
                                                                                  long maxUnixTimeSeconds,
                                                                                  long intervalUnixTimeSeconds,
                                                                                  FillStrategy fillStrategy)
        {
            IEnumerable<TimeAndValue> result = default;

            await collector.IterateGraphValues(refId, minUnixTimeSeconds, maxUnixTimeSeconds, CollectAndGroup).ConfigureAwait(false);
            return result;

            // this is called under db lock
            void CollectAndGroup(IEnumerable<TimeAndValue> x)
            {
                var timeSeriesHelper = new TimeSeriesHelper(minUnixTimeSeconds, maxUnixTimeSeconds, intervalUnixTimeSeconds, x);
                result = timeSeriesHelper.ReduceSeriesWithAverage(fillStrategy);
            }
        }
    }
}