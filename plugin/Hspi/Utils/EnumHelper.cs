using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

#nullable enable

namespace Hspi.Utils
{
    internal static class EnumHelper
    {
        public static IEnumerable<T> GetValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }
    }
}