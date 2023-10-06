using Hspi.Database;

#nullable enable

namespace Hspi
{
    public interface ITimeAndValueList
    {
        public TimeAndValue this[int index] { get; }

        public bool IsValidIndex(int index);
    }
}