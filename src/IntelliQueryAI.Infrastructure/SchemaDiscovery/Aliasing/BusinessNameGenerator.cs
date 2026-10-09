using System.Text.RegularExpressions;

namespace IntelliQueryAI.Infrastructure.SchemaDiscovery.Aliasing;

/// <summary>
/// Utility that automatically transforms raw database table, column, and routine identifiers
/// into clean, human-readable, business-friendly labels (e.g. 'tbl_customer' -> 'Customer').
/// </summary>
public static class BusinessNameGenerator
{
    private static readonly string[] CommonPrefixes =
    [
        "tbl_", "tbl", "tb_", "t_", "vw_", "v_", "sp_", "usp_", "fn_", "sys_"
    ];

    private static readonly Dictionary<string, string> CommonAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "cust", "Customer" },
        { "usr", "User" },
        { "emp", "Employee" },
        { "dept", "Department" },
        { "org", "Organization" },
        { "prod", "Product" },
        { "cat", "Category" },
        { "qty", "Quantity" },
        { "amt", "Amount" },
        { "desc", "Description" },
        { "addr", "Address" },
        { "num", "Number" },
        { "dt", "Date" },
        { "tm", "Time" },
        { "id", "ID" },
        { "fk", "Foreign Key" },
        { "pk", "Primary Key" },
        { "cd", "Code" },
        { "pct", "Percentage" },
        { "val", "Value" },
        { "curr", "Currency" },
        { "txn", "Transaction" },
        { "inv", "Invoice" }
    };

    /// <summary>
    /// Converts a technical database name into an executive-ready business alias.
    /// </summary>
    public static string ToFriendlyName(string technicalName)
    {
        if (string.IsNullOrWhiteSpace(technicalName))
        {
            return string.Empty;
        }

        string clean = technicalName.Trim();

        // 1. Strip common database prefixes (case-insensitive)
        foreach (var prefix in CommonPrefixes)
        {
            if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && clean.Length > prefix.Length)
            {
                clean = clean[prefix.Length..];
                break;
            }
        }

        // 2. Handle snake_case or kebab-case
        clean = clean.Replace('_', ' ').Replace('-', ' ');

        // 3. Handle PascalCase or camelCase by inserting spaces before uppercase letters
        clean = Regex.Replace(clean, @"([a-z0-9])([A-Z])", "$1 $2");
        clean = Regex.Replace(clean, @"([a-zA-Z])([0-9]+)", "$1 $2");

        // 4. Tokenize and expand known business abbreviations
        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titleWords = new List<string>(words.Length);

        foreach (var word in words)
        {
            if (CommonAbbreviations.TryGetValue(word, out var expansion))
            {
                titleWords.Add(expansion);
            }
            else if (word.Length == 1)
            {
                titleWords.Add(word.ToUpperInvariant());
            }
            else
            {
                // Capitalize first letter, lowercase rest
                titleWords.Add(char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());
            }
        }

        return string.Join(" ", titleWords);
    }
}
