# Meta Catalog Integration Specification

## 1. Objective

The Meta integration will synchronize products managed in Velonixs Connect with a connected Meta WhatsApp Catalog so that customers can browse and order the same products inside WhatsApp experiences.

## 2. Goals

- Keep catalog data consistent between Velonixs and Meta
- Support product create, update, and delete events
- Track sync health and retry failures
- Securely store Meta authentication settings
- Prepare for future automated product publishing workflows

## 3. Prerequisites

Before enabling Meta catalog sync, the business must have:
- Meta Business Manager access
- A WhatsApp Business Account (WABA)
- A Meta app configured for WhatsApp business flows
- A system user with appropriate permissions
- A permanent access token
- A catalog ID
- A business ID
- A phone number ID

## 4. Configuration

The system should store the following settings securely:
- Business ID
- WABA ID
- Catalog ID
- Phone Number ID
- Access Token
- Sync Enabled flag
- Default sync behavior

The API-wide WhatsApp verify token and app secret validate inbound webhooks. They are not per-business catalog routing settings.

Recommended storage:
- MetaSettings table for configuration
- CatalogSyncQueue table for pending work items
- CatalogSyncLogs table for request and response history

## 5. Sync Model

### 5.1 Supported Events
- Create product
- Update product
- Delete product
- Retry failed sync

### 5.2 Sync Status Values
- Pending
- Waiting
- Processing
- Paused
- Synced
- Simulated
- Cancelled
- Failed

### 5.3 Product Mapping

Each Velonixs product should map to Meta product data using:
- Product name
- Description
- Price
- Category / product type
- Availability
- Image URL
- Business-specific product identifier

## 6. Integration Flow

1. An admin creates or updates a product in Velonixs.
2. The product is stored in SQL Server.
3. The product is queued for synchronization.
4. A background worker picks up pending records.
5. The worker calls the Meta catalog API with the product payload.
6. The returned Meta product identifier is stored.
7. The product sync status is updated to Synced or Failed.

At most one deliverable event and one waiting successor exist per product. A successor is released only after the predecessor reaches a terminal state, preserving remote create/update/delete order.

## 7. API and Worker Design

### 7.1 Background Processing

A background job should run periodically, for example every minute, to:
- Read pending records from the sync queue
- Send product payloads to Meta
- Update outcome status
- Retry failed items using exponential backoff

### 7.2 Recommended Implementation

Use a hosted background service or queue-based processing framework such as:
- IHostedService
- Hangfire
- Quartz

### 7.3 Retry Strategy

Retry rules should include:
- Immediate retry for transient network errors
- Exponential backoff for repeated failures
- Max retry limit per item
- Failure reason capture in logs

## 8. Image Handling

Before sending product images to Meta:
- Upload the image to a public or accessible storage location
- Store the resulting public HTTPS URL
- Send that URL during the catalog sync request

Image requirements:
- Valid HTTPS URL
- Supported image format
- Reasonable file size

## 9. Error Handling

The integration should handle and log errors for:
- Invalid or expired token
- Invalid image URL
- Missing category or item data
- Network timeout
- Meta rate limiting
- Invalid catalog ID or business configuration

Each failure should be recorded with:
- Product identifier
- Attempt number
- Error message
- Timestamp
- HTTP status code when available

## 10. Security Requirements

- Encrypt stored access tokens
- Restrict Meta settings to platform administrators
- Audit every sync request and response
- Log operator actions around manual sync attempts
- Avoid exposing secrets in logs or UI responses

## 11. Data Model

### Suggested Tables
- MetaSettings
- CatalogSyncQueue
- CatalogSyncLogs

### Suggested Fields

MetaSettings:
- Id
- BusinessId
- CatalogId
- WabaId
- PhoneNumberId
- AccessTokenEncrypted
- WebhookVerifyTokenEncrypted
- IsEnabled
- CreatedAt
- UpdatedAt

CatalogSyncQueue:
- Id
- BusinessId
- ProductId
- ProductRetailerId
- EventType
- PayloadJson
- Status
- RetryCount
- NextAttemptAt
- PredecessorQueueItemId
- LeaseId
- LeaseExpiresAt
- CreatedAt
- UpdatedAt

CatalogSyncLogs:
- Id
- ProductId
- EventType
- Status
- ResponseCode
- ErrorMessage
- CreatedAt

## 12. Implementation Plan

### Phase 1 - Configuration
- Add secure Meta settings storage
- Add admin configuration UI fields
- Validate catalog and business identifiers

### Phase 2 - Sync Queue
- Create product sync queue records on create/update/delete
- Add status tracking and retry handling

### Phase 3 - Worker Execution
- Build background processing for pending items
- Send product payloads to Meta
- Save returned Meta product identifiers

### Phase 4 - Monitoring
- Add logs, failure visibility, and manual retry tools
- Add resiliency and alerting for repeated failures

## 13. Acceptance Criteria

The Meta integration is complete when:
- Products created or updated in Velonixs can be queued for sync
- Sync status is visible in the admin UI
- Successful syncs save Meta product identifiers
- Failed syncs are retried and logged
- Tokens and secrets are stored securely
- Product images are included in the catalog payload when available
