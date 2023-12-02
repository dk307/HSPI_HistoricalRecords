using System;
using Hspi.Utils;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class HsDeviceInvalidExceptionTests
    {
        [Test]
        public void DefaultConstructor()
        {
            // Arrange
            HsDeviceInvalidException exception = new();
            Assert.That(exception, Is.InstanceOf<Exception>());
        }

        [Test]
        public void MessageAndInnerExceptionConstructor()
        {
            // Arrange
            string errorMessage = "Test Error Message";
            Exception innerException = new("Inner Exception");
            HsDeviceInvalidException exception = new(errorMessage, innerException);

            // Act & Assert
            Assert.That(exception, Is.InstanceOf<Exception>());
            Assert.That(exception.Message, Is.EqualTo(errorMessage));
            Assert.That(exception.InnerException, Is.EqualTo(innerException));
        }

        [Test]
        public void MessageConstructor()
        {
            // Arrange
            string errorMessage = "Test Error Message";
            HsDeviceInvalidException exception = new(errorMessage);

            // Act & Assert
            Assert.That(exception, Is.InstanceOf<Exception>());
            Assert.That(exception.Message, Is.EqualTo(errorMessage));
        }
    }
}