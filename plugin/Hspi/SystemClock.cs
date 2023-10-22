using System;

#nullable enable

namespace Hspi
{
    public interface ISystemClock
    {
        DateTimeOffset Now { get; }
    };

    public class SystemClock : ISystemClock
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;
    };
}