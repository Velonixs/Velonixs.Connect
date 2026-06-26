namespace Velonixs.Connect.Application.Models;

public sealed record MenuImportResult(
    int CreatedCategories,
    int CreatedItems,
    int UpdatedItems,
    IReadOnlyCollection<MenuImportRowError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

public sealed record MenuImportRowError(
    int RowNumber,
    string Message);
