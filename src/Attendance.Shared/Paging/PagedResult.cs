namespace Attendance.Shared.Paging;

/// <summary>One page of a list, plus what the client needs to render a pager.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="TotalCount">Items across all pages that match the filters.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) => new([.. Items.Select(map)], Page, PageSize, TotalCount);
}

public enum SortDirection
{
    Asc,
    Desc,
}
