namespace Velonixs.Connect.Persistence.Security;

public interface IFieldEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string protectedValue);
}
