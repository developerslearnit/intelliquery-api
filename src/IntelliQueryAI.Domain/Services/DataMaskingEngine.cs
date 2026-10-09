using System.Security.Cryptography;
using System.Text;
using IntelliQueryAI.Domain.Entities.Governance;
using IntelliQueryAI.Domain.Enums;

namespace IntelliQueryAI.Domain.Services;

public interface IDataMaskingEngine
{
    string MaskValue(object? rawValue, MaskingStrategy strategy, string? pattern = null);

    IReadOnlyDictionary<string, object?> MaskRow(
        IReadOnlyDictionary<string, object?> row,
        string? tableName,
        IReadOnlyList<GovernancePolicy> policies,
        string userRole,
        bool isAiContext);

    IReadOnlyList<IReadOnlyDictionary<string, object?>> MaskRows(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string? tableName,
        IReadOnlyList<GovernancePolicy> policies,
        string userRole,
        bool isAiContext);

    string TestMask(string sampleValue, MaskingStrategy strategy, string? pattern = null);
}

public sealed class DataMaskingEngine : IDataMaskingEngine
{
    public string MaskValue(object? rawValue, MaskingStrategy strategy, string? pattern = null)
    {
        if (rawValue is null) return string.Empty;
        string str = rawValue.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(str)) return string.Empty;

        return strategy switch
        {
            MaskingStrategy.None => str,
            MaskingStrategy.Full => "********",
            MaskingStrategy.Partial => MaskPartial(str),
            MaskingStrategy.Email => MaskEmail(str),
            MaskingStrategy.Hash => MaskHash(str),
            MaskingStrategy.Nullify => "[RESTRICTED]",
            _ => "********"
        };
    }

    public string TestMask(string sampleValue, MaskingStrategy strategy, string? pattern = null)
    {
        return MaskValue(sampleValue, strategy, pattern);
    }

    public IReadOnlyDictionary<string, object?> MaskRow(
        IReadOnlyDictionary<string, object?> row,
        string? tableName,
        IReadOnlyList<GovernancePolicy> policies,
        string userRole,
        bool isAiContext)
    {
        if (policies.Count == 0 || row.Count == 0) return row;

        var masked = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in row)
        {
            string colName = kvp.Key;
            object? val = kvp.Value;

            // Find first matching active policy for this column
            var policy = policies.FirstOrDefault(p => p.Matches(tableName, colName));
            if (policy != null && policy.MaskingStrategy != MaskingStrategy.None)
            {
                bool shouldMask = false;

                if (isAiContext)
                {
                    // For AI Prompts: if AI masking is enforced, ALWAYS mask to prevent data leakage.
                    // Otherwise, only unmask if role is allowed.
                    shouldMask = policy.IsAiMaskingEnforced || !policy.IsRoleAllowed(userRole);
                }
                else
                {
                    // For User presentation: mask if the user's role is not in the allowed roles list.
                    shouldMask = !policy.IsRoleAllowed(userRole);
                }

                if (shouldMask)
                {
                    val = MaskValue(val, policy.MaskingStrategy, policy.MaskingPattern);
                }
            }

            masked[colName] = val;
        }

        return masked;
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> MaskRows(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string? tableName,
        IReadOnlyList<GovernancePolicy> policies,
        string userRole,
        bool isAiContext)
    {
        if (policies.Count == 0 || rows.Count == 0) return rows;

        var result = new List<IReadOnlyDictionary<string, object?>>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(MaskRow(row, tableName, policies, userRole, isAiContext));
        }

        return result;
    }

    private static string MaskPartial(string input)
    {
        if (input.Length <= 4)
        {
            return new string('*', Math.Max(input.Length, 4));
        }

        int maskLength = input.Length - 4;
        return new string('*', maskLength) + input[^4..];
    }

    private static string MaskEmail(string input)
    {
        int atIndex = input.IndexOf('@');
        if (atIndex <= 1)
        {
            return MaskPartial(input);
        }

        string localPart = input[..atIndex];
        string domainPart = input[atIndex..];

        return $"{localPart[0]}***{domainPart}";
    }

    private static string MaskHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        string hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"[HASH:{hex[..10]}]";
    }
}
