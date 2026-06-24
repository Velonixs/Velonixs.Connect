namespace Velonixs.Connect.Domain.Entities;

public sealed class PlatformTaxSetting
{
    public const string DefaultId = "GST";

    public string Id { get; set; } = DefaultId;
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
