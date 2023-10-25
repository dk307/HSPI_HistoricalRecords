using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public static class Initialize
    {
        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            Logger.ConfigureLogging(Serilog.Events.LogEventLevel.Debug, false);

            Log.Information("Starting Tests");
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            Log.Information("Finishing Tests");
        }
    }
}