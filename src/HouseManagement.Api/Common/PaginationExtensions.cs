namespace HouseManagement.Api.Common;

public static class PaginationExtensions
{
    public const int DefaultPageSize = 50;
    public const int MaximumPageSize = 100;

    public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, int? page, int? pageSize)
    {
        var effectivePage = page.GetValueOrDefault() > 0 ? page!.Value : 1;
        var requestedPageSize = pageSize.GetValueOrDefault();
        var effectivePageSize = requestedPageSize > 0
            ? Math.Min(requestedPageSize, MaximumPageSize)
            : DefaultPageSize;
        var skip = Math.Min((long)(effectivePage - 1) * effectivePageSize, int.MaxValue);

        return query
            .Skip((int)skip)
            .Take(effectivePageSize);
    }
}
