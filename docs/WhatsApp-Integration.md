# WhatsApp Integration

## Current Flow

1. Customer sends a WhatsApp message.
2. Meta posts the message to `POST /api/webhooks/whatsapp`.
3. The API resolves the configured business by WhatsApp phone number id.
4. The conversation engine replies with welcome, catalog, cart, checkout, or confirmation messages.
5. Confirmed orders notify staff through WhatsApp when sending is enabled.

## Configuration

```text
WhatsApp__ApiVersion=v20.0
WhatsApp__VerifyToken=
WhatsApp__AccessToken=
WhatsApp__AppSecret=
WhatsApp__DisableSending=false
```

## Staff Notification Note

Indian 10-digit staff numbers are normalized to country-code format before sending to Meta. For example, `8080225080` is sent as `918080225080`.
