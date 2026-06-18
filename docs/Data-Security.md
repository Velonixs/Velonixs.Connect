# Data Security

## Current Controls

- ASP.NET Identity hashes user passwords. Passwords are never stored reversibly.
- Admin and Business Portal access requires authenticated, role-authorized sessions.
- Portal queries are scoped to the signed-in user's business.
- SQL connection credentials, JWT signing keys, WhatsApp credentials, SMTP passwords, webhook tokens, and the data-encryption key are loaded from Secret Manager, environment variables, or a production secret store.
- SQL connections request encrypted transport.
- Sensitive non-searchable database fields use AES-256-GCM authenticated encryption with a random nonce per value.
- Existing plaintext values are rewritten once after the encryption migration and recorded in `DataProtectionState`.
- Logs avoid customer message bodies, phone numbers, notification bodies, provider responses, and business phone-number IDs.

## Encrypted Database Fields

```text
Restaurant.BusinessPhone
Restaurant.NotificationEmail
Restaurant.StaffWhatsAppNumber
Restaurant.Address
Customer.Name
Customer.LastAddress
Conversation.WhatsAppNumber
Conversation.TempOrderJson
Order.CustomerName
Order.CustomerPhone
Order.Address
MessageLog.MessageText
```

Ciphertext is stored as a versioned value beginning with `enc:v1:`. Legacy plaintext can still be read during migration, but all new or updated values are encrypted.

## Searchable Fields

These fields remain plaintext because current workflows perform indexed equality lookups:

```text
Customer.PhoneNumber
Restaurant.WhatsAppPhoneNumberId
Identity email and normalized email
```

The next hardening step is to add keyed HMAC lookup columns, migrate equality queries to those hashes, and then encrypt the original customer phone-number value. Identity email protection requires a broader login and lookup design.

## Key Management

Use one shared `DataEncryption__Key` for API, Admin, and Portal instances that access the same database. It must be a Base64-encoded 32-byte key.

Never commit encryption keys. Losing the key makes encrypted data unrecoverable. Replacing it without a key-rotation migration also makes existing data unreadable.

For production:

- Store secrets and encryption keys in Azure Key Vault.
- Use managed identity where possible.
- Back up keys separately from database backups.
- Restrict Key Vault access to the application identities.
- Configure SQL Server with a valid TLS certificate and use `Encrypt=True;TrustServerCertificate=False`.
- Rotate WhatsApp access tokens, SMTP credentials, database credentials, and JWT signing keys after any suspected exposure.
