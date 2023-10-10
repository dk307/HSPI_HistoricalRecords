using System;

#nullable enable

namespace Hspi
{
    internal interface ISystemClock
    {
        DateTimeOffset Now { get; }
    };

    internal class SystemClock : ISystemClock
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;
    };
}