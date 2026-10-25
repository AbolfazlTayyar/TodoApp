namespace TodoApp.Shared.Abstractions.Pagination;

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

/// <summary>
/// Never return unbounded lists from an API. A table that is small today will hold millions of rows later.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
}
