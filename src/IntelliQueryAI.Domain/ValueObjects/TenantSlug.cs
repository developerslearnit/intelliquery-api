using System.Text.RegularExpressions;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.SharedKernel.Primitives;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.ValueObjects;

/// <summary>
/// Represents a unique, URL-safe tenant workspace slug (e.g. 'acme' in 'acme.aiquery.com').
/// </summary>
public sealed class TenantSlug : ValueObject
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);
    public const int MinLength = 3;
    public const int MaxLength = 63;

    private static readonly string[] ReservedSubdomains =
        ["admin", "api", "app", "auth", "billing", "dashboard", "dev", "docs", "portal", "root", "status", "system", "www"];

    /// <summary>
    /// Gets the normalized slug string.
    /// </summary>
    public string Value { get; }

    private TenantSlug(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Required for EF Core deserialization.
    /// </summary>
    private TenantSlug()
    {
        Value = string.Empty;
    }

    /// <summary>
    /// Generates a valid URL-safe tenant slug from an arbitrary input name or phrase.
    /// Cleans non-alphanumeric characters, enforces length constraints [3..63],
    /// and resolves collisions with reserved subdomains.
    /// </summary>
    /// <param name="name">The raw name or phrase to slugify.</param>
    /// <returns>A guaranteed valid slug string.</returns>
    public static string Generate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "workspace";
        }

        string clean = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-");
        clean = Regex.Replace(clean, @"-+", "-").Trim('-');

        if (clean.Length > MaxLength)
        {
            clean = clean[..MaxLength].TrimEnd('-');
        }

        if (clean.Length < MinLength)
        {
            clean = clean.Length switch
            {
                0 => "workspace",
                1 => $"{clean}-ws",
                2 => $"{clean}-w",
                _ => clean
            };
        }

        if (ReservedSubdomains.Contains(clean))
        {
            clean = $"{clean}-ws";
            if (clean.Length > MaxLength)
            {
                clean = clean[..MaxLength].TrimEnd('-');
            }
        }

        return clean;
    }

    /// <summary>
    /// Creates a new <see cref="TenantSlug"/> instance after validating business invariants.
    /// </summary>
    /// <param name="slug">The raw slug string.</param>
    /// <returns>A successful Result containing the slug, or a validation failure.</returns>
    public static Result<TenantSlug> Create(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<TenantSlug>(WorkspaceErrors.SlugRequired);
        }

        string normalized = slug.Trim().ToLowerInvariant();

        if (normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            return Result.Failure<TenantSlug>(WorkspaceErrors.SlugInvalidLength(MinLength, MaxLength));
        }

        if (!SlugRegex.IsMatch(normalized))
        {
            return Result.Failure<TenantSlug>(WorkspaceErrors.SlugInvalidFormat);
        }

        // Reserved subdomains
        if (ReservedSubdomains.Contains(normalized))
        {
            return Result.Failure<TenantSlug>(WorkspaceErrors.SlugReserved(normalized));
        }

        return Result.Success(new TenantSlug(normalized));
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
    public static implicit operator string(TenantSlug slug) => slug.Value;
}
