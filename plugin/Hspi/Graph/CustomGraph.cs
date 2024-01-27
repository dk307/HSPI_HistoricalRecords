using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.Graph
{
    internal sealed record CustomGraph(int Id, string Name, ImmutableDictionary<int, CustomGraphLine> Lines)
    {
    }
}