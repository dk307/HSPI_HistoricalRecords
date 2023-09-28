using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hspi
{
    public record TimeAndValue(DateTimeOffset TimeStamp, double DeviceValue);

    public enum ResultSortBy
    {
        TimeDesc = 0,
        ValueDesc = 1,
        StringDesc = 2,
        TimeAsc = 3,
        ValueAsc = 4,
        StringAsc = 5,
    }

    internal interface IDatabaseCollector
    {
        Task Record(RecordData recordData);

        IList<RecordData> GetRecords(int refId, TimeSpan timeSpan,
                                     int start, int length, ResultSortBy sortBy);

        long GetRecordsCount(int refId, TimeSpan timeSpan);

        IList<TimeAndValue> GetGraphValues(int refId, DateTimeOffset min, DateTimeOffset max);
    }
}