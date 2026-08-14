# MVP Definition

Scope of this document: the smallest release of **notify-api** that delivers complete value.
Product context and boundaries: [PRODUCT.md](PRODUCT.md).

The MVP is not a list of features. It is one journey that an actor can complete from beginning to
end. Everything below exists to make that journey work, or is explicitly excluded.

## The main journey

Two actors cooperate in a single journey; neither half has value alone.

**Setup journey — tenant administrator (human)**

```
Register tenant + admin account
  → log in
  → configure an email sender (SMTP host, credentials, from-address)
  → send a test message and see it arrive
  → create a message template with variables
  → create an API key for a producer application
```

**Sending journey — producer application (machine)**

```
Call the service with API key + template key + recipient + variables
  → get an immediate acknowledgement (message accepted, id returned)
  → the service renders the template and sends the email
  → the recipient receives the email
  → the administrator looks the message up and sees it was sent
  → if it failed, the administrator sees why and retries it
```

The journey is complete when a producer application that has written no sending code causes a real
email to reach a real recipient, and a human can afterwards confirm that it happened.

## Must have

Without any one of these, the journey above does not work.

| # | Capability | Why the journey needs it |
|---|-----------|--------------------------|
| M-01 | Tenant registration, admin login (session token) | Nothing can be configured without an owner |
| M-02 | Tenant isolation on every read and write | Configuration, messages and history belong to one tenant |
| M-03 | API key issue + revoke, scoped to the tenant | The producer application must authenticate as a machine |
| M-04 | Email sender configuration: SMTP host/port/credentials/from-address, credentials encrypted, never read back | The service cannot send without a sending account |
| M-05 | Test-send from a stored sender configuration | The administrator must be able to confirm the configuration is correct before wiring an application to it |
| M-06 | Template: create/read/update, subject + body, `{{variable}}` substitution | Content ownership by the tenant is the point of the product |
| M-07 | Accept endpoint: template key, one recipient, variables → persist and acknowledge immediately | The producer's half of the journey |
| M-08 | Asynchronous delivery worker: render, send via the configured sender, record the outcome | Accepting without sending is not value |
| M-09 | Retry with backoff on transient failure; permanent failure recorded, not retried | An outage must not lose the message (M1/M5 in PRODUCT.md) |
| M-10 | Message status lookup by id, including attempts, timestamps, and failure reason | Answers "did it arrive?" without log access |
| M-11 | Message history list for the tenant, filterable by status | The administrator's diagnosis step |
| M-12 | Manual retry of a failed message | Closes the loop after an outage |
| M-13 | Request validation with clear errors (unknown template, missing variable, bad address) | A producer must be able to integrate without guessing |
| M-14 | Per-tenant accept rate limit | Prevents one producer from taking down the shared service |

## Should have

Real value, deliberately released after the MVP.

| # | Capability | Why it can wait |
|---|-----------|-----------------|
| S-01 | Idempotency key on accept, de-duplicating retries by the producer | At-least-once is accepted for the MVP (assumption A5) |
| S-02 | Batch accept (many messages in one call) | Single-message calls cover the first integrations |
| S-03 | Multiple recipients (to/cc/bcc) per message | One recipient completes the journey |
| S-04 | HTML body alongside plain text | Plain text proves delivery; formatting is a follow-up |
| S-05 | API-based email provider (SES/SendGrid) in addition to SMTP | SMTP works everywhere; the second adapter validates the abstraction |
| S-06 | Bounce/complaint feedback from the provider (`delivered` / `bounced` states) | Requires the API provider first |
| S-07 | Attachments | Not required by the first notifications |
| S-08 | Multiple sender configurations per tenant with per-message selection | One sender per tenant is assumed sufficient (A6) |
| S-09 | Template versioning and preview | Editing in place is workable at low volume |
| S-10 | Retention policy job (delete message bodies after N days) | Needed before volume grows; not before first value |

## Could have

Improves the experience, does not decide the MVP.

| # | Capability |
|---|-----------|
| C-01 | Web console for configuration and history (MVP is API-only) |
| C-02 | Bulk retry of all failures in a time range |
| C-03 | Per-tenant dashboards and delivery-rate charts |
| C-04 | Template import/export and cross-tenant sharing |
| C-05 | Alerting when a tenant's failure rate crosses a threshold |
| C-06 | Client SDK for producer applications |
| C-07 | OpenAPI specification published from the running service |

## Not now

Actively excluded from this version. These are inherited from the product-level non-goals in
[PRODUCT.md](PRODUCT.md) and must not be reintroduced as "we might need it later".

| # | Excluded |
|---|----------|
| N-01 | Channels other than email (SMS, push, chat, generic webhooks) |
| N-02 | Recipient preference centre, unsubscribe handling, quiet hours, digests |
| N-03 | Marketing/campaign sending, segmentation, open/click tracking |
| N-04 | In-application notification inbox for end users |
| N-05 | Scheduled or recurring sends |
| N-06 | The service deciding *when* to notify (event subscription, business rules) |
| N-07 | A contact/recipient directory owned by the service |
| N-08 | Any integration with the CDN service's database, tenants or accounts |
| N-09 | Multi-region deployment, high-availability topology, autoscaling |
| N-10 | Recipient-facing user interface |

## MVP completion criteria

The MVP is done when all of the following hold.

**Journey**

- [ ] A tenant administrator completes the setup journey through the API alone, with no manual
      database or server access.
- [ ] A producer application, holding only an API key and the documentation, causes a real email to
      reach a real inbox.
- [ ] After a deliberately induced sender outage, the messages accepted during the outage are sent
      once the sender recovers, with no manual intervention.
- [ ] A permanently failed message is visible with its reason and can be retried by the
      administrator.

**Authorization and isolation**

- [ ] Every endpoint requires authentication; no endpoint returns tenant data without it.
- [ ] Cross-tenant access is verified as impossible for every resource (sender configuration,
      template, message, history, API key) — a test exercises each with another tenant's identity.
- [ ] Sender credentials are encrypted at rest and are not returned by any endpoint, including error
      messages and logs.
- [ ] A revoked API key stops working immediately.

**Data**

- [ ] Database backup and restore is documented and has been executed successfully at least once on
      a copy with real-shaped data.
- [ ] Schema changes are migrations, and each migration has been applied and rolled back once.
- [ ] Restoring a backup does not resend already-sent messages.

**Operations**

- [ ] Health endpoint reports the service plus its database and queue dependencies.
- [ ] Structured logs carry a request/message correlation id, and 5xx responses never leak internal
      messages to callers.
- [ ] Failed deliveries and worker crashes surface somewhere a human watches — not only in logs.
- [ ] Minimum metrics are visible: accepted count, sent count, failed count, queue depth.

**Rollout and rollback**

- [ ] The service and its worker deploy as their own units, independent of the CDN service.
- [ ] A deployment can be rolled back to the previous version, and the rollback path for a schema
      change is documented for each migration.
- [ ] Configuration is environment variables only; no secret is baked into an image.
- [ ] Restarting the worker mid-flight loses no accepted message.

**Documentation**

- [ ] A producer integration guide exists: authenticate, send, read status, interpret errors.
- [ ] Every documented error code is returned by the implementation, and every returned error code
      is documented.
