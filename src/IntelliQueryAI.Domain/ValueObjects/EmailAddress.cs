using System.Net.Mail;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.SharedKernel.Primitives;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.ValueObjects;

/// <summary>
/// Represents a normalized and validated email address.
/// </summary>
public sealed class EmailAddress : ValueObject
{
    public const int MaxLength = 256;

    /// <summary>
    /// Gets the normalized email string.
    /// </summary>
    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Required for EF Core.
    /// </summary>
    private EmailAddress()
    {
        Value = string.Empty;
    }

    /// <summary>
    /// Creates and validates an email address.
    /// </summary>
    /// <param name="email">The raw email address string.</param>
    /// <returns>A successful Result containing the EmailAddress, or a validation error.</returns>
    public static Result<EmailAddress> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<EmailAddress>(IdentityErrors.EmailRequired);
        }

        string trimmed = email.Trim().ToLowerInvariant();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<EmailAddress>(IdentityErrors.EmailTooLong(MaxLength));
        }

        if (!MailAddress.TryCreate(trimmed, out var mailAddress) || mailAddress.Address != trimmed)
        {
            return Result.Failure<EmailAddress>(IdentityErrors.EmailInvalidFormat);
        }

        return Result.Success(new EmailAddress(trimmed));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Implicit string conversion operator.
    /// </summary>
    public static implicit operator string(EmailAddress email) => email.Value;
}
