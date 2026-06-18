using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Persistence.Configuration;

namespace Velonixs.Connect.Persistence.Security;

public sealed class AesGcmFieldEncryptionService : IFieldEncryptionService
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmFieldEncryptionService(IOptions<DataEncryptionOptions> options)
        : this(options.Value.Key)
    {
    }

    public AesGcmFieldEncryptionService(string configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "Data encryption key is not configured. Set DataEncryption:Key or DATA_ENCRYPTION_KEY.");
        }

        try
        {
            _key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("DataEncryption:Key must be a Base64-encoded 256-bit key.", exception);
        }

        if (_key.Length != 32)
        {
            throw new InvalidOperationException("DataEncryption:Key must decode to exactly 32 bytes.");
        }
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || plaintext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return plaintext;
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue) || !protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return protectedValue;
        }

        var payload = Convert.FromBase64String(protectedValue[Prefix.Length..]);

        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Encrypted field payload is invalid.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
