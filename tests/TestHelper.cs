using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HomeKitControllerTest
{
    internal class TestHelper
    {
        public static void VerifyHtmlValid(string html)
        {
            HtmlAgilityPack.HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(html);
            Assert.AreEqual(0, htmlDocument.ParseErrors.Count());
        }
    }
}