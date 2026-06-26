namespace Velonixs.Connect.Contracts.Common;

public sealed record PaginationRequest(int PageNumber = 1, int PageSize = 25)
{
    public const int MaxPageSize = 100;

    public int NormalizedPageNumber => Math.Max(1, PageNumber);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
