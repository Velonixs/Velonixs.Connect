namespace Velonixs.Connect.Persistence.Persistence;

public sealed class DataProtectionState
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}
