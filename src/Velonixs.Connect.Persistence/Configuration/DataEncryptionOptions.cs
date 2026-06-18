namespace Velonixs.Connect.Persistence.Configuration;

public sealed class DataEncryptionOptions
{
    public const string SectionName = "DataEncryption";

    public string Key { get; set; } = string.Empty;
}
