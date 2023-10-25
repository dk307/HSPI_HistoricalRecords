using System;

#nullable enable

namespace Hspi.Utils
{
    internal static class TypeConverter
    {
        public static T? TryGetFromObject<T>(object? value) where T : struct
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}