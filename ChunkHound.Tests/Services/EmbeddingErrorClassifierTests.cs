using Xunit;
using ChunkHound.Services;
using ChunkHound.Core;
using System.Net;

namespace ChunkHound.Tests.Services;

/// <summary>
/// Tests for the EmbeddingErrorClassifier class, which handles error classification for embedding operations.
/// </summary>
public class EmbeddingErrorClassifierTests
{
    private readonly EmbeddingErrorClassifier _classifier = new();

    /// <summary>
    /// Tests that TimeoutException is correctly classified as a transient error.
    /// This validates proper handling of timeout scenarios that should be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_TimeoutException_ReturnsTransient()
    {
        // Arrange
        var exception = new TimeoutException("Request timed out");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that OperationCanceledException is correctly classified as a transient error.
    /// This validates proper handling of cancellation scenarios that should be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_OperationCanceledException_ReturnsTransient()
    {
        // Arrange
        var exception = new OperationCanceledException("Operation was canceled");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that HttpRequestException with HTTP 500 status code is classified as transient.
    /// This validates proper handling of server errors that should be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_HttpRequestException_ServerError_ReturnsTransient()
    {
        // Arrange
        var httpException = new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError);
        var exception = new Exception("Network error", httpException);

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that HttpRequestException with HTTP 429 (Too Many Requests) is classified as transient.
    /// This validates proper handling of rate limiting that should be retried with backoff.
    /// </summary>
    [Fact]
    public void ClassifyError_HttpRequestException_TooManyRequests_ReturnsTransient()
    {
        // Arrange
        var httpException = new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);
        var exception = new Exception("Rate limited", httpException);

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that HttpRequestException with HTTP 401 (Unauthorized) is classified as permanent.
    /// This validates proper handling of authentication errors that should not be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_HttpRequestException_ClientError_ReturnsPermanent()
    {
        // Arrange
        var httpException = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);
        var exception = new Exception("Auth failed", httpException);

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Permanent, result);
    }

    /// <summary>
    /// Tests that HttpRequestException with HTTP 404 (Not Found) is classified as permanent.
    /// This validates proper handling of resource not found errors that should not be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_HttpRequestException_NotFound_ReturnsPermanent()
    {
        // Arrange
        var httpException = new HttpRequestException("Not Found", null, HttpStatusCode.NotFound);
        var exception = new Exception("Resource not found", httpException);

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Permanent, result);
    }

    /// <summary>
    /// Tests that HttpRequestException without status code but with connection-related message is transient.
    /// This validates proper handling of network connectivity issues that should be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_HttpRequestException_ConnectionError_ReturnsTransient()
    {
        // Arrange
        var httpException = new HttpRequestException("Connection reset");
        var exception = new Exception("Network issue", httpException);

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that exceptions with timeout-related messages are classified as transient.
    /// This validates message-based classification for timeout scenarios.
    /// </summary>
    [Fact]
    public void ClassifyError_TimeoutMessage_ReturnsTransient()
    {
        // Arrange
        var exception = new Exception("The request timed out after 30 seconds");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that exceptions with rate limit messages are classified as transient.
    /// This validates message-based classification for rate limiting scenarios.
    /// </summary>
    [Fact]
    public void ClassifyError_RateLimitMessage_ReturnsTransient()
    {
        // Arrange
        var exception = new Exception("Rate limit exceeded, please try again later");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that exceptions with throttle messages are classified as transient.
    /// This validates message-based classification for throttling scenarios.
    /// </summary>
    [Fact]
    public void ClassifyError_ThrottleMessage_ReturnsTransient()
    {
        // Arrange
        var exception = new Exception("Request throttled due to high load");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that exceptions with service unavailable messages are classified as transient.
    /// This validates message-based classification for service availability issues.
    /// </summary>
    [Fact]
    public void ClassifyError_ServiceUnavailableMessage_ReturnsTransient()
    {
        // Arrange
        var exception = new Exception("Service temporarily unavailable");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that exceptions with connection-related messages are classified as transient.
    /// This validates message-based classification for connectivity issues.
    /// </summary>
    [Fact]
    public void ClassifyError_ConnectionMessage_ReturnsTransient()
    {
        // Arrange
        var exception = new Exception("Connection was closed by the remote host");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that exceptions with unknown error messages are classified as permanent.
    /// This validates default classification for unrecognized error patterns.
    /// </summary>
    [Fact]
    public void ClassifyError_UnknownError_ReturnsPermanent()
    {
        // Arrange
        var exception = new Exception("Some unknown error occurred");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Permanent, result);
    }

    /// <summary>
    /// Tests that inner exceptions are properly classified when outer exception doesn't match patterns.
    /// This validates recursive error classification through exception chains.
    /// </summary>
    [Fact]
    public void ClassifyError_WithInnerException_UsesInnerException()
    {
        // Arrange
        var innerException = new TimeoutException("Inner timeout");
        var outerException = new Exception("Wrapper exception", innerException);

        // Act
        var result = _classifier.ClassifyError(outerException);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Transient, result);
    }

    /// <summary>
    /// Tests that ArgumentException is classified as permanent.
    /// This validates proper handling of argument validation errors that should not be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_ArgumentException_ReturnsPermanent()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Permanent, result);
    }

    /// <summary>
    /// Tests that InvalidOperationException is classified as permanent.
    /// This validates proper handling of operation errors that should not be retried.
    /// </summary>
    [Fact]
    public void ClassifyError_InvalidOperationException_ReturnsPermanent()
    {
        // Arrange
        var exception = new InvalidOperationException("Invalid operation");

        // Act
        var result = _classifier.ClassifyError(exception);

        // Assert
        Assert.Equal(EmbeddingErrorClassification.Permanent, result);
    }
}