# Preliminary Feature Map

Purpose: break the MVP journey into the concrete capabilities the product must provide, at enough
detail to decide the architecture — no further.

Inputs: [MVP.md](MVP.md) (journey and Must-have list M-01…M-14), [domain-map.md](domain-map.md)
(domains and invariants).

Deliberately **not** decided here: the full endpoint list, database fields, acceptance criteria,
screens. Those belong to SPECS.md, written after the architecture.

## Levels — what this document is about

| Level | Example | Where it is decided |
|-------|---------|--------------------|
| Domain | Notification Intake | domain-map.md |
| **Feature** | **Accept a notification request** | **this document** |
| Contract | `POST /v1/notifications` | SPECS.md |
| Implementation | `NotificationService.accept()` | code |

Only the feature level is settled below. Contracts appear nowhere in this file.

## Feature map

Legend: **[MVP]** required for the end-to-end journey · **[later]** deliberately after the MVP.

```
Identity & Access
├── Register a tenant with its first administrator      [MVP]  M-01
├── Log in / refresh a session                          [MVP]  M-01
├── Issue a machine key                                 [MVP]  M-03
├── List machine keys (never the secret)                [MVP]  M-03
├── Revoke a machine key                                [MVP]  M-03
├── Authenticate a caller (human or machine)            [MVP]  M-01/M-03
├── Enforce the tenant boundary on every operation      [MVP]  M-02
├── Manage additional administrators                    [later]
└── Audit configuration changes                         [later]

Sender Configuration
├── Configure an email sender (host, account, from)     [MVP]  M-04
├── Store the sending secret write-only                 [MVP]  M-04
├── View sender settings without the secret             [MVP]  M-04
├── Prove a sender by sending a test message            [MVP]  M-05
├── Update / disable a sender                           [MVP]  M-04
├── Several senders per tenant, one default             [later] S-08
└── API-based provider besides SMTP                     [later] S-05

Message Content  (helper, not a gate — producers may supply their own wording)
├── Create a template (key, subject, text body)         [MVP]  M-06
├── Read / list templates                               [MVP]  M-06
├── Update a template                                   [MVP]  M-06
├── Declare and validate its variables                  [MVP]  M-06/M-13
├── Render a template with supplied data                [MVP]  M-06
├── HTML body                                           [later] S-04
├── Template versioning and preview                     [later] S-09
└── Withdraw a template                                 [later]

Notification Intake
├── Accept a request: validate, persist, acknowledge    [MVP]  M-07
├── Accept wording supplied with the request            [MVP]  M-07
├── Reject an invalid request with a usable error       [MVP]  M-13
├── Resolve which sender applies (and template, if named) [MVP] M-07
├── Keep the rendered content with the request          [MVP]  M-07/M-10
├── Rate-limit a tenant's intake                        [MVP]  M-14
├── De-duplicate by idempotency key                     [later] S-01
├── Accept a batch                                      [later] S-02
├── Several recipients per request                      [later] S-03
├── Attachments                                         [later] S-07
└── Scheduled send                                      [not now] N-05

Delivery
├── Take accepted work asynchronously                   [MVP]  M-08
├── Hand the rendered message to the sender             [MVP]  M-08
├── Record the outcome of each attempt                  [MVP]  M-08
├── Retry transient failures with backoff               [MVP]  M-09
├── Fail permanent refusals without retrying            [MVP]  M-09
├── Give up after the attempt limit, with a reason      [MVP]  M-09
├── Re-attempt on human request                         [MVP]  M-12
├── Ingest provider feedback (bounce / confirmed)       [later] S-06
└── Channels other than email                           [not now] N-01

History & Audit
├── Look up one notification with its attempts          [MVP]  M-10
├── List a tenant's notifications, filter by status     [MVP]  M-11
├── Expose health and basic counters                    [MVP]  completion criteria
├── Retention / deletion of stored content              [later] S-10
├── Failure-rate alerting                               [later] C-05
└── Dashboards                                          [later] C-03
```

Every Must-have item M-01…M-14 appears exactly once above, and no MVP feature exists that no
Must-have asks for.

## Dependencies

```
Identity & Access ──────────────────────────────┐
        │                                       │ (authenticates and scopes everything)
        ▼                                       ▼
Sender Configuration            Message Content
        │                                │
        │  (which account sends)         │ (wording + variables)
        │                                ▼
        │                        Notification Intake ◀── producer application
        │                                │
        │                                │ (accepted work)
        └───────────────▶  Delivery ◀────┘
                                │
                                ▼
                        History & Audit ◀── administrator
```

Reading order for build sequence: Identity & Access has no dependency and must exist first. Sender
Configuration and Message Content are independent of each other and can be built in parallel.
Intake needs Sender Configuration, and Message Content only when a template is named.
Delivery needs Intake and Sender Configuration. History depends on records the
others produce and can only be finished last.

Two directions worth stating because they are easy to get wrong:

- Sender Configuration must not know that notifications exist. It is asked for a sender; it does not
  reach into intake or delivery.
- Message Content must not know how a message leaves. Rendering is pure: template plus data in,
  finished text out.

## Vertical slice — the first thing to build

The journey works end to end with a subset of the MVP features. Building this slice first proves the
architecture before the rest is filled in:

```
Register tenant → log in → configure SMTP sender → create template
    → issue machine key → accept one request → render → send once
    → look up the outcome
```

Deferred inside the MVP, added straight after the slice: test-send, retry and backoff, manual retry,
rate limiting, history listing and filtering.

## Assumptions used here

These are working assumptions so the map could be drawn; they are the questions still open in
[domain-map.md](domain-map.md) and may change the map.

| # | Assumption | What changes if it is wrong |
|---|-----------|----------------------------|
| F1 | One sender per tenant in the MVP | Intake gains a "choose a sender" feature and templates may need to declare one |
| F4 | Retry is a human action only | Delivery would need a producer-facing retry feature and its own authorisation rule |

Decided since (see [domain-map.md](domain-map.md#decisions-taken)) and already reflected above:

| Decision | Effect on this map |
|----------|-------------------|
| A tenant is the owning organisation; each source system is a producer with its own key | No second isolation level; history filtering and rate limits are per producer key |
| Producers supply the finished subject and body | Templates stay in the map but stop being on the mandatory path; intake gained "accept wording supplied with the request" |
| One request, one channel; email only in v1 | No fan-out feature; a later channel adds a kind of sender, not a new notification shape |

## What this is enough for

The architecture can now decide: where the boundary between accepting and delivering runs, which
parts must be durable, which parts run outside the request path, and what the provider abstraction
must hide. It cannot yet decide contracts — that is SPECS.md, after the architecture is agreed.
