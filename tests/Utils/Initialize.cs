using Hspi;
using NUnit.Framework;
using Serilog;

namespace HSPI_HistoryTest
{
    [SetUpFixture]
    public class Initialize
    {
        [OneTimeSetUp]
        public void AssemblyInitialize()
        {
            Logger.ConfigureLogging(Serilog.Events.LogEventLevel.Debug, false);

            Log.Information("Starting Tests");
        }

        [OneTimeTearDown]
        public void AssemblyCleanup()
        {
            Log.Information("Finishing Tests");
        }
    }
}