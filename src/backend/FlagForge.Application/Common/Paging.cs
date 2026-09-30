namespace FlagForge.Application.Common;

public static class Paging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static IQueryable<T> Page<T>(this IQueryable<T> query, int page, int pageSize) =>
        query.Skip((page - 1) * pageSize).Take(pageSize);
}
