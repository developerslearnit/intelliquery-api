namespace IntelliQueryAI.SharedKernel.Results;

/// <summary>
/// Represents a paginated list of items with pagination metadata.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage)
{
    /// <summary>
    /// Creates a new instance of <see cref="PagedResult{T}"/> with computed pagination properties.
    /// </summary>
    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : pageSize;
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)safePageSize);

        return new PagedResult<T>(
            items,
            totalCount,
            safePage,
            safePageSize,
            totalPages,
            safePage > 1,
            safePage < totalPages);
    }
}
