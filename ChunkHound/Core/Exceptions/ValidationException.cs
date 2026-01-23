namespace ChunkHound.Core.Exceptions;

/// <summary>
/// Exception thrown when model validation fails.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Gets the name of the field that failed validation.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Gets the value that failed validation.
    /// </summary>
    public object? FieldValue { get; }

    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    public ValidationException(string fieldName, object? fieldValue, string message)
        : base(message)
    {
        FieldName = fieldName;
        FieldValue = fieldValue;
    }

    /// <summary>
    /// Initializes a new instance of the ValidationException class with a message.
    /// </summary>
    public ValidationException(string message)
        : base(message)
    {
        FieldName = string.Empty;
        FieldValue = null;
    }
}