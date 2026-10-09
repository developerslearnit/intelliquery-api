namespace IntelliQueryAI.Domain.Enums;

/// <summary>
/// Data sensitivity classification hierarchy.
/// </summary>
public enum DataClassification
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3,
    Pii = 4,
    Financial = 5,
    Secret = 6
}

/// <summary>
/// Data masking transformation strategy.
/// </summary>
public enum MaskingStrategy
{
    None = 0,
    Full = 1,
    Partial = 2,
    Email = 3,
    Hash = 4,
    Nullify = 5
}
