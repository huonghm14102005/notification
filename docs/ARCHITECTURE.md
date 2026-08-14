# Architecture

Technical shape of notify-api, derived from [PRODUCT.md](PRODUCT.md), [MVP.md](MVP.md),
[domain-map.md](domain-map.md) and [feature-map.md](feature-map.md).

This document decides structure, boundaries and mechanisms. It does not define endpoints, request
bodies or table columns — those belong to SPECS.md, written next.

## 1. Context

```
  University source systems                notify-api                     Outside world
 ┌──────────────────────┐          ┌───────────────────────────┐
 │ Grades system        │          │  api        (HTTP)        │
 │ Conduct-points system│──HTTP───▶│    accept, configure,     │        ┌──────────────┐
 │ (later) error logs   │  API key │    inspect                │        │ SMTP account │
 └──────────────────────┘          │        │                  │───────▶│ of the       │
                                   │        ▼ hand-off         │        │ university   │
 ┌──────────────────────┐          │  worker     (no HTTP in)  │        └──────┬───────┘
 │ Administrator        │──HTTP───▶│    render, send, retry    │               │
 │ (human, browser/curl)│  session └───────────┬───────────────┘               ▼
 └──────────────────────┘                      │                            recipient
                                     PostgreSQL │ Redis
```

Two deployable units, one codebase, one database. Nothing is shared with the CDN/Media service:
separate repository, separate database, separate identities.

## 2. Decisions

| # | Decision | Reason | Rejected alternative |
|---|----------|--------|---------------------|
| D1 | Standalone service, not a module of cdn-api | Producers are other systems; the notification path must not be coupled to media deployments | A `notification` module in cdn-api — cheaper to ship, but ties releases and tenancy together |
| D2 | Own tenancy, own credentials, own database | Decided by the product owner; the CDN service's tenants are not the university's source systems | Reusing cdn-api's tenants/api_keys |
| D3 | Split into `api` and `worker` processes over a shared queue | Invariant I5/I6: accept durably, deliver later; a provider outage must not touch the accept path | Sending inline in the request — fails M3 (p95 < 100 ms) and loses messages on provider downtime |
| D4 | PostgreSQL as the record of truth; Redis only as the work queue | History and outcomes must survive a queue flush; queues are not durable storage | Queue as the source of truth |
| D5 | Job carries only the notification id | Content can be large and can change; the worker re-reads the row it is about to act on | Full payload in the job |
| D6 | One delivery attempt = one immutable row (I12) | Diagnosis and audit need every try, not the last one | Overwriting a status field |
| D7 | Provider access behind a narrow `EmailSender` port | A second provider (S-05) and a later channel must not touch intake | Calling an SMTP client directly from the delivery logic |
| D8 | Sender secrets encrypted with an application key, never returned (I4) | The service holds the university's real mail credentials | Storing plaintext and relying on database access control |
| D9 | Content arrives with the request; templates are optional | Product decision: producers send their own subject/body | Mandatory templates |
| D10 | Same stack and conventions as the CDN service (Node + Fastify + TypeScript + Kysely, Docker Compose, Nginx) | One team, one operational model; the conventions are already written down | A different runtime, which would double the operational surface |

## 3. Processes

### `api` — the only inbound surface

Stateless HTTP. Responsibilities: authenticate, authorise inside a tenant, validate, read and write
Postgres, enqueue work. It never talks to a mail provider, with one exception: the sender test
(M-05) sends synchronously, because the administrator is waiting for the answer.

### `worker` — everything that may be slow or fail

Consumes the delivery queue. Responsibilities: load the notification, render if a template was named,
call the sender port, write the attempt row, decide retry or give up. It exposes no HTTP except a
health endpoint. Horizontal scaling is adding worker instances; concurrency is bounded per instance
so a provider is not flooded.

Crash safety: a job is only acknowledged after the attempt row is committed. A worker killed
mid-send may cause the same message to be sent twice — accepted under assumption A5 (at-least-once),
and reduced later by the idempotency work (S-01).

## 4. Module boundaries in code

One module per domain, following the CDN service's convention
(`{module}.route.ts` → `{module}.service.ts` → `{module}.repository.ts` → `{module}.schema.ts`).

```
src/
  modules/
    identity/      tenants, administrators, sessions, machine keys
    sender/        sender records, secret handling, verification
    template/      templates, variables, rendering (pure)
    notification/  intake: validate, persist, enqueue; status and history reads
    delivery/      attempts, retry policy, orchestration of a send
  providers/
    email/         smtp.ts implements the EmailSender port; index.ts selects one
  lib/             db, queue, crypto, logger, errors, pagination
  worker/          queue consumer wiring delivery
```

Rules that keep the domain boundaries real:

- A route never reaches a repository directly; a service never reaches another module's repository.
- `template` is pure: given wording and data it returns text. It performs no I/O and knows nothing
  about senders or notifications.
- `sender` exposes "give me a usable sender for this tenant" and nothing about notifications.
- Only `delivery` imports from `providers/`.
- Every repository function takes a tenant id and filters on it — isolation is enforced at the
  lowest layer, not in routes (I1, I2).

## 5. Data ownership and durability

| Store | Holds | Durability requirement |
|-------|-------|------------------------|
| PostgreSQL | Tenants, administrators, machine keys, senders (secret encrypted), templates, notifications with their content and recipient, delivery attempts | Record of truth; backup and restore is an MVP completion criterion |
| Redis | Delivery jobs, retry scheduling, rate-limit counters | Rebuildable: losing Redis loses in-flight scheduling, not messages |

Because Redis is rebuildable, a notification that is `accepted` but has no job must be recoverable. A
periodic sweep re-enqueues notifications left in a non-terminal state past a threshold — this is what
makes I6 ("always reaches a terminal outcome") true rather than aspirational.

## 6. The delivery path

```
producer ─▶ api: authenticate (machine key → tenant + producer id)
              ├─ validate request                       ─┐ reject here leaves no record (I8)
              ├─ resolve sender for the tenant           │
              ├─ resolve template if one was named       │
              ├─ persist notification (status accepted)  ├─ all inside one transaction
              └─ enqueue { notificationId }             ─┘   commit, then enqueue
            └▶ 202 with the notification id                  (sweep covers an enqueue that fails)

worker ─▶ load notification (must be accepted or retrying)
            ├─ mark in progress
            ├─ render if needed
            ├─ open sender, send
            ├─ write attempt row (succeeded | failed + classification)
            └─ on failure: transient → reschedule with backoff, up to the limit
                           permanent → terminal failed, with reason (I13, I14)
```

Failure classification is a decision of the provider adapter, not of the delivery service: the
adapter maps SMTP responses to `transient` or `permanent`, so adding a provider does not change the
retry logic.

Manual retry (M-12) creates a new attempt on the same notification; it never rewrites history (I16,
I17).

## 7. Security

- Two credential kinds: administrator session (short-lived token) and machine key (`notify_` +
  random, stored hashed, prefix for lookup). Revocation is immediate (I3).
- Every request resolves a tenant before anything else; a request that cannot be attributed to a
  tenant is rejected before validation.
- Sender secrets are encrypted at rest with a key from the environment, decrypted only in the worker
  at send time, and excluded from every serialiser, log and error message.
- Producers now supply their own content (D9), which makes a compromised key able to send arbitrary
  text from the university's address. The MVP mitigation is a per-key rate limit plus full
  attribution of every notification to the key that created it; a stricter restriction is an open
  point in domain-map.md.
- Rate limiting is per tenant and per key, counted in Redis, applied before any database write.

## 8. Operations

- **Health**: `api` and `worker` each report their own liveness plus database and queue reachability.
- **Logs**: structured, one correlation id per request, carried into the job so a notification can be
  followed from accept to attempt. 5xx bodies never carry internal messages.
- **Metrics**: accepted, sent, failed, queue depth, attempts per outcome class.
- **Deployment**: Docker Compose alongside the existing stack; `api` behind Nginx, `worker` with no
  inbound route. The two images are the same build with a different entrypoint, so they cannot drift.
- **Rollout**: schema migrations run before the new version starts; each migration has a documented
  reverse step. `api` and `worker` can be rolled back independently, which is why the job payload is
  only an id (D5) — an old worker can still process a new job.

## 9. Deliberately not in this architecture

Fan-out inside a notification (one request is one channel), scheduling, a contact directory, an
in-app inbox, a console, multi-region or high-availability topology. Each is a product-level non-goal
and none may be reintroduced as a technical convenience.

## 10. What the specification must settle next

Endpoints and their contracts; the exact state names persisted; table columns and indexes; the retry
limit and backoff values; the rate-limit numbers; error codes; the sender-verification procedure.
