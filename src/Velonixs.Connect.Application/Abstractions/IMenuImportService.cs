using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IMenuImportService
{
    Task<MenuImportResult> ImportExcelAsync(
        Guid restaurantId,
        Stream workbookStream,
        CancellationToken cancellationToken = default);
}
