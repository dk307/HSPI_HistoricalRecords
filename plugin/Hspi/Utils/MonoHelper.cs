using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

#nullable enable

namespace Hspi.Hspi.Utils
{
    internal static class MonoHelper
    {
        public static bool IsRunningOnMono() => Type.GetType("Mono.Runtime") is not null;

        public static string? GetMonoDisplayString()
        {
            var type = Type.GetType("Mono.Runtime");
            if (type != null)
            {
                var method = type.GetMethod("GetDisplayName", BindingFlags.Static | BindingFlags.NonPublic);
                if (method != null)
                {
                    return method.Invoke(null, null) as string;
                }
            }

            return null;
        }

        public static Version? GetMonoVersion()
        {
            var displayVersion = GetMonoDisplayString();
            if (displayVersion != null)
            {
                var versionString = displayVersion.Split(' ').FirstOrDefault()?.Trim();

                if (versionString != null && Version.TryParse(versionString, out var version))
                {
                    return version;
                }
            }

            return null;
        }
    }
}