# Product Brief

Product: **notify-api** — a standalone notification service, built from scratch, sitting next to the
existing CDN/Media service and sharing none of its code or data.

## Problem

When something happens inside one of our applications, the people who care about it are not told
reliably. Each application that needs to reach a person today has to solve the same set of problems
on its own: where the sending credentials live, what the message looks like, what happens when the
mail host is down, and how anyone finds out afterwards whether the message actually arrived.

The consequences we observe:

- Sending logic is re-implemented per application, so a change of provider or sender domain means
  touching every application.
- Sending happens inline with the user request, so a slow or unavailable provider degrades the
  application itself.
- A failed send is usually a log line in one application, invisible to everyone else. Nobody can
  answer "did the customer get it?" without reading server logs.
- Credentials for the sending account are copied into every application that sends.

## Target users

| Actor | Role | Primary need |
|-------|------|--------------|
| **Producer application** (machine — primary actor) | An internal service that has just handled an event and must inform a person | One call that reliably takes responsibility for the message |
| Tenant administrator (human) | Owns an application/product line and its sending configuration | Configure sender accounts and message content; see what was sent and what failed |
| Platform operator (human) | Runs the service | See throughput and failures across all tenants; know when a provider is unhealthy |

Message recipients (end users, tenant staff) are affected by the product but do not interact with it
in v1.

## Value proposition

Applications hand off "tell this person this thing" as a single call and stop caring about the rest.
Delivery, retries, credentials, content and history become one shared responsibility instead of a
duplicated one, so an application team can ship a new notification without owning any sending
infrastructure, and a business owner can change sender accounts or wording without a code deploy.

## Main user outcomes

1. A producer application informs a person without embedding any sending logic or credentials, and
   its own latency and availability are unaffected by the sending path.
2. A message that cannot be sent immediately is still sent later, without the producer doing
   anything, and the producer can find out afterwards whether it succeeded.
3. A tenant administrator changes message wording or the sending account themselves, without a code
   change in any producer application.
4. Anyone with access can answer, for a specific message, "was it sent, when, through what, and if
   not, why" — without reading application logs.
5. Retrying a batch of failures after an outage is a deliberate action someone can take, not a
   rewrite of history.

## Success metrics

| # | Metric | Definition | Target |
|---|--------|-----------|--------|
| M1 | Accepted-message loss | Messages accepted by the service that reach no terminal state | 0 |
| M2 | Eventual send rate | Accepted messages that are sent, excluding hard rejections by the provider (invalid address) | > 99% |
| M3 | Producer-visible latency | p95 time for the accept call | < 100 ms |
| M4 | Time to first attempt | p95 from accept to first send attempt | < 5 s |
| M5 | Recovery after provider outage | Share of messages queued during a 30-minute provider outage that are sent within 15 minutes of recovery | 100% |
| M6 | Self-service content change | Share of message-wording changes made without a producer code deploy | > 90% |
| M7 | Adoption | Producer applications integrated within one quarter of launch | ≥ 3 |
| M8 | Diagnosis effort | Median steps for an administrator to determine the fate of a specific message | 1 query, no log access |

## Constraints

**Technical**

- Standalone service with its own datastore and its own identity/tenancy; it must not read or write
  the CDN service's database, and must be deployable and restartable independently.
- Runs on the same host stack as the existing service (Docker Compose, PostgreSQL, Redis, Nginx) and
  follows the repository's documented conventions and workflow rules.
- v1 delivers over email only; the design must not make a second channel a rewrite.
- Sending credentials belong to the tenant, are encrypted at rest and are never readable back out.
- Throughput must survive a provider being unavailable for tens of minutes without data loss.

**Time and budget**

- No dedicated infrastructure budget: reuse the existing PostgreSQL/Redis/Compose deployment.
- Third-party email provider cost must stay within the sending volume the business already pays for.
- Small team, incremental delivery: a usable first version before any second channel is considered.

**Legal / compliance**

- Message content and recipient addresses are personal data: retention must be bounded and the
  content of a message must be deletable on request.
- Transactional messages only in v1. Marketing/bulk sending would pull in consent and unsubscribe
  obligations we are not taking on.
- Tenant data must be isolated; no tenant may see another tenant's messages, recipients or
  credentials.

## Assumptions

Unvalidated — each needs confirmation before we depend on it.

| # | Assumption | How we would validate |
|---|-----------|----------------------|
| A1 | Producer teams will adopt a central service rather than keep their own sending code | Commitment from the first two producer teams before build |
| A2 | Email covers the great majority of near-term notification need | Inventory of notifications the existing applications want to send |
| A3 | Tenant administrators want to own message content, and will actually edit it | Interview the intended administrators |
| A4 | Expected volume fits comfortably on the shared PostgreSQL/Redis stack | Estimate volume with the producer teams; load-test the accept path |
| A5 | At-least-once delivery is acceptable — a rare duplicate email is preferable to a lost one | Confirm with the producer teams per notification type |
| A6 | One sending account per tenant is enough; per-application sender identities are not required yet | Ask the tenant administrators |
| A7 | Recipients are supplied by the producer; the service does not need its own directory of people | Review the intended use cases |
| A8 | Scheduled/future-dated sending is not needed for the first version | Review the intended use cases |

## Risks

| # | Risk | Impact | Mitigation |
|---|------|--------|-----------|
| R1 | Producers keep their own sending path and the service is bypassed | Product delivers no value | Land the first producer as part of v1; make integration cheaper than the status quo |
| R2 | Deliverability (spam classification, domain reputation) is worse than what applications had | Users stop receiving messages, trust is lost | Own sender-domain authentication (SPF/DKIM/DMARC) explicitly; monitor bounce/complaint rates from day one |
| R3 | A central service becomes a single point of failure for every application | Broad outage | Accept-and-queue design so producers are never blocked; independent deployment; explicit degradation behaviour |
| R4 | Leak or misuse of stored sending credentials | Security incident, sending on behalf of a tenant | Encryption at rest, write-only credential handling, per-tenant isolation, audit of configuration changes |
| R5 | Producer becomes an open relay: anything can send arbitrary content to anyone | Abuse, reputation damage | Authenticated producers, per-tenant rate limits, controlled message content |
| R6 | Storing message bodies and recipients creates a personal-data liability | Compliance exposure | Bounded retention, deletion on request, minimise what is stored |
| R7 | Scope creeps into a marketing/campaign platform | Never ships | Non-goals below are enforced; transactional-only in v1 |
| R8 | Building "from scratch" duplicates auth/tenancy that already exists elsewhere | Wasted effort, inconsistent operations | Deliberate decision, recorded here; revisit only if operating two identity models proves painful |

## Product-level non-goals

The product deliberately does **not** address the following. Each may be reconsidered later, but no
v1 decision may be justified by it.

1. **Recipient preference management** — no per-person preference centre, no unsubscribe handling,
   no digest/quiet-hours logic.
2. **Marketing and campaign sending** — no audience segmentation, no bulk campaigns, no A/B testing,
   no engagement analytics (opens, clicks).
3. **Channels other than email in v1** — no SMS, push, chat platforms or generic webhooks.
4. **An in-application notification inbox** — the product does not store or serve a feed of
   notifications for end users to read inside a product.
5. **Deciding when to notify** — the product does not observe events or apply business rules to
   decide that a notification is warranted; producers decide, the product delivers.
6. **A directory of people** — the product does not own a contact database; recipients arrive with
   the request.
7. **A recipient-facing user interface** — v1 is consumed by applications and administrators, not by
   message recipients.
8. **Scheduled or recurring sending** — no future-dated or repeating messages in v1.
9. **Replacing the CDN/Media service's own concerns** — the two services stay separate products.
