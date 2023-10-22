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
            HsDeviceInvalidException exception = new HsDeviceInvalidException();

            // Act & Assert
            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception, typeof(Exception));
        }

        [TestMethod]
        public void MessageAndInnerExceptionConstructor()
        {
            // Arrange
            string errorMessage = "Test Error Message";
            Exception innerException = new Exception("Inner Exception");
            HsDeviceInvalidException exception = new HsDeviceInvalidException(errorMessage, innerException);

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
            HsDeviceInvalidException exception = new HsDeviceInvalidException(errorMessage);

            // Act & Assert
            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception, typeof(Exception));
            Assert.AreEqual(errorMessage, exception.Message);
        }
    }
}