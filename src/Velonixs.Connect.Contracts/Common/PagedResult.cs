namespace Velonixs.Connect.Contracts.Common;

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}
