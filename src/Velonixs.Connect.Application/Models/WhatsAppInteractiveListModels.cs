namespace Velonixs.Connect.Application.Models;

public sealed record WhatsAppInteractiveListSection(
    string Title,
    IReadOnlyCollection<WhatsAppInteractiveListRow> Rows);

public sealed record WhatsAppInteractiveListRow(
    string Id,
    string Title,
    string? Description = null);
