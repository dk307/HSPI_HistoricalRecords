using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace HSPI_HistoryTest
{
    [TestFixture]
    public class ShippedDllsTest
    {
        [Test]
        public void InstallFileHasAllDlls()
        {
            string path = GetInstallFilePath();
            var dllFilesPaths = Directory.GetFiles(path, "*.dll");
            Assert.That(dllFilesPaths, Is.Not.Empty);

            var dllFiles = dllFilesPaths.Select(x => Path.GetFileName(x)).ToList();

            // files not shipped
            dllFiles.Remove("HSCF.dll");
            dllFiles.Remove("PluginSdk.dll");
            dllFiles.Remove("HomeSeerAPI.dll");

            // Parse shipped dlls
            var installDlls = File.ReadLines(Path.Combine(path, "DllsToShip.txt")).ToList();

            Assert.That(dllFiles, Is.EquivalentTo(installDlls), "Dlls in output is not same as shipped dlls");
        }

        private static string GetInstallFilePath()
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            var parentDirectory = new DirectoryInfo(Path.GetDirectoryName(dllPath));
            return Path.Combine(parentDirectory.Parent.Parent.Parent.FullName, "plugin", "bin", "Debug");
        }
    }
}