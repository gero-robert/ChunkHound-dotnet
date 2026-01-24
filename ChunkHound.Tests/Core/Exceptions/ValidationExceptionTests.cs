using Xunit;
using ChunkHound.Core.Exceptions;

namespace ChunkHound.Core.Tests.Exceptions
{
    public class ValidationExceptionTests
    {
        [Fact]
        public void Constructor_WithFieldDetails_SetsPropertiesCorrectly()
        {
            // Arrange
            var fieldName = "TestField";
            var fieldValue = "invalid value";
            var message = "Validation failed";

            // Act
            var exception = new ValidationException(fieldName, fieldValue, message);

            // Assert
            Assert.Equal(fieldName, exception.FieldName);
            Assert.Equal(fieldValue, exception.FieldValue);
            Assert.Equal(message, exception.Message);
        }

        [Fact]
        public void Constructor_WithMessageOnly_SetsDefaultProperties()
        {
            // Arrange
            var message = "Validation failed";

            // Act
            var exception = new ValidationException(message);

            // Assert
            Assert.Equal(string.Empty, exception.FieldName);
            Assert.Null(exception.FieldValue);
            Assert.Equal(message, exception.Message);
        }

        [Fact]
        public void Constructor_WithNullFieldValue_SetsFieldValueToNull()
        {
            // Arrange
            var fieldName = "TestField";
            object? fieldValue = null;
            var message = "Validation failed";

            // Act
            var exception = new ValidationException(fieldName, fieldValue, message);

            // Assert
            Assert.Equal(fieldName, exception.FieldName);
            Assert.Null(exception.FieldValue);
            Assert.Equal(message, exception.Message);
        }
    }
}