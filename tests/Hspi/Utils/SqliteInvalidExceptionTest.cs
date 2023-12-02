using System;
using Hspi.Database;
using NUnit.Framework;

namespace HSPI_HistoricalRecordsTest
{
    [TestFixture]
    public class SqliteInvalidExceptionTests
    {
        [Test]
        public void DefaultConstructor()
        {
            // Arrange
            SqliteInvalidException exception = new();

            // Act & Assert
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception, Is.InstanceOf<Exception>());
        }

        [Test]
        public void MessageAndInnerExceptionConstructor()
        {
            // Arrange
            string errorMessage = "Test Error Message";
            Exception innerException = new("Inner Exception");
            SqliteInvalidException exception = new(errorMessage, innerException);

            // Act & Assert
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception, Is.InstanceOf<Exception>());
            Assert.That(exception.Message, Is.EqualTo(errorMessage));
            Assert.That(exception.InnerException, Is.EqualTo(innerException));
        }

        [Test]
        public void MessageConstructor()
        {
            // Arrange
            string errorMessage = "Test Error Message";
            SqliteInvalidException exception = new(errorMessage);

            // Act & Assert
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception, Is.InstanceOf<Exception>());
            Assert.That(exception.Message, Is.EqualTo(errorMessage));
        }
    }
}