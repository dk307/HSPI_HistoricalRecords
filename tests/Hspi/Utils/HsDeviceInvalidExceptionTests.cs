using System;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class HsDeviceInvalidExceptionTests
    {
        [TestMethod]
        public void DefaultConstructor()
        {
            // Arrange
            HsDeviceInvalidException exception = new();

            // Act & Assert
            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception, typeof(Exception));
        }

        [TestMethod]
        public void MessageAndInnerExceptionConstructor()
        {
            // Arrange
            string errorMessage = "Test Error Message";
            Exception innerException = new("Inner Exception");
            HsDeviceInvalidException exception = new(errorMessage, innerException);

            // Act & Assert
            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception, typeof(Exception));
            Assert.AreEqual(errorMessage, exception.Message);
            Assert.AreEqual(innerException, exception.InnerException);
        }

        [TestMethod]
        public void MessageConstructor()
        {
            // Arrange
            string errorMessage = "Test Error Message";
            HsDeviceInvalidException exception = new(errorMessage);

            // Act & Assert
            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception, typeof(Exception));
            Assert.AreEqual(errorMessage, exception.Message);
        }
    }
}