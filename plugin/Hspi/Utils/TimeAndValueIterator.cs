using System;
using System.Collections.Generic;
using Hspi.Database;

#nullable enable

namespace Hspi
{
    internal class TimeAndValueQueryHelper
    {
    }

    internal sealed class TimeAndValueIterator
    {
        public TimeAndValueIterator(IEnumerable<TimeAndValue> list, long maxUnixTimeSeconds)
        {
            this.maxUnixTimeSeconds = maxUnixTimeSeconds;
            this.listEnumertor = list.GetEnumerator();

            if (listEnumertor.MoveNext())
            {
                current = listEnumertor.Current;
            }
            if (listEnumertor.MoveNext())
            {
                next = listEnumertor.Current;
            }
        }

        public TimeAndValue Current => current ?? throw new IndexOutOfRangeException("Current is invalid");

        public long FinishTimeForCurrentTimePoint
        {
            get
            {
                if (IsNextValid)
                {
                    return Math.Min(Next.UnixTimeSeconds, maxUnixTimeSeconds);
                }
                else
                {
                    return Math.Max(maxUnixTimeSeconds, Current.UnixTimeSeconds);
                }
            }
        }

        public bool IsCurrentValid => current != null;
        public bool IsNextValid => next != null;
        public TimeAndValue Next => next ?? throw new IndexOutOfRangeException("Next is invalid");

        public void MoveNext()
        {
            if (next != null)
            {
                current = next;
                next = null;
            }
            else
            {
                current = null;
            }

            if (listEnumertor.MoveNext())
            {
                next = listEnumertor.Current;
            }
        }

        private readonly IEnumerator<TimeAndValue> listEnumertor;
        private readonly long maxUnixTimeSeconds;
        private TimeAndValue? current;
        private TimeAndValue? next;
    }
}