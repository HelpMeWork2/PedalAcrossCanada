namespace PedalAcrossCanada.Shared.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> data, int page, int pageSize, int totalCount) =>
        new() { Data = data, Page = page, PageSize = pageSize, TotalCount = totalCount };

    public static PagedResult<T> Empty(int page = 1, int pageSize = 25) =>
        new() { Data = [], Page = page, PageSize = pageSize, TotalCount = 0 };
}
