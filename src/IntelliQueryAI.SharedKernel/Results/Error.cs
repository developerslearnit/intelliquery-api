namespace IntelliQueryAI.SharedKernel.Results;

/// <summary>
/// Categorizes the domain or operation error for HTTP status mapping and diagnostics.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Failure due to violation of business validation rules (HTTP 400).
    /// </summary>
    Validation = 0,

    /// <summary>
    /// Resource requested does not exist (HTTP 404).
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Attempted state transition or operation conflicts with current state (HTTP 409).
    /// </summary>
    Conflict = 2,

    /// <summary>
    /// Caller is not authenticated or credentials are invalid (HTTP 401).
    /// </summary>
    Unauthorized = 3,

    /// <summary>
    /// Caller is authenticated but lacks required permission (HTTP 403).
    /// </summary>
    Forbidden = 4,

    /// <summary>
    /// Generic or unrecoverable domain failure (HTTP 500).
    /// </summary>
    Failure = 5
}

/// <summary>
/// Represents a structured domain or application error.
/// </summary>
public sealed record Error
{
    /// <summary>
    /// Unique machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable error description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The categorization of the error.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Represents no error (null object pattern).
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>
    /// Represents a null-value error.
    /// </summary>
    public static readonly Error NullValue = new("Error.NullValue", "The specified value is null.", ErrorType.Validation);

    private Error(string code, string description, ErrorType type)
    {
        Code = code;
        Description = description;
        Type = type;
    }

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    /// <summary>
    /// Creates a generic failure error.
    /// </summary>
    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
}
