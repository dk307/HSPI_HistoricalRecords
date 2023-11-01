using HomeSeer.PluginSdk;

using static System.FormattableString;

namespace Hspi.Utils
{
    internal static class HsHelper
    {
        public static string GetNameForLog(IHsController hsController, int refId)
        {
            try
            {
                return Invariant($"{hsController.GetNameByRef(refId)}({refId})");
            }
            catch
            {
                return Invariant($"RefId:{refId}");
            }
        }
    }
}