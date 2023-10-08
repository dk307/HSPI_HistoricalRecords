using System.Collections.Generic;
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
}