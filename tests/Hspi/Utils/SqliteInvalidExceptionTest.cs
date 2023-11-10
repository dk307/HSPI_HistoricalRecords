using System;
using Hspi.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSPI_HistoricalRecordsTest
{
    [TestClass]
    public class SqliteInvalidExceptionTests
    {
        [TestMethod]
        public void DefaultConstructor()
        {
            // Arrange
            SqliteInvalidException exception = new();

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
            SqliteInvalidException exception = new(errorMessage, innerException);

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
            SqliteInvalidException exception = new(errorMessage);

            // Act & Assert
            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception, typeof(Exception));
            Assert.AreEqual(errorMessage, exception.Message);
        }
    }
}