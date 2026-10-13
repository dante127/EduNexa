namespace SchoolOS.Application.Common;

/// <summary>Standard paged response envelope: { items, page, pageSize, total }.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
