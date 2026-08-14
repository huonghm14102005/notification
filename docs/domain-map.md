# Domain Map

Purpose: identify areas of business responsibility before the system is cut into technical
components. A domain here is a conceptual boundary, not a service and not a database.

Derived from the MVP journey in [MVP.md](MVP.md). Terminology decisions still open are collected in
[Open points](#open-points).

## 1. Extraction from the journey

The journey, restated as business steps:

```
An administrator takes ownership of a workspace
  → describes how messages leave the system (a sending account)
  → proves that account works
  → writes the wording of a message, with blanks to fill in
  → grants a machine permission to ask for messages to be sent
A producer application asks for one message to be sent, filling in the blanks
  → the request is taken on, with a promise to deliver
  → the wording is turned into a finished message
  → the finished message is handed to the sending account
  → the recipient receives it
  → someone later asks what happened to that request
  → and asks again, after a failure
```

### Business nouns

| Concept | Behaviour |
|---------|-----------|
| Tenant | Owns everything else; the boundary of visibility |
| Administrator | A human who configures a tenant and inspects its history |
| Producer | A machine caller acting for a tenant; not a person |
| Credential | Proves who is calling — a human session or a machine key |
| Sender | A configured way for messages to leave (host, account, from-address) |
| Template | Reusable wording with named blanks |
| Recipient | The person a message is aimed at; supplied per request, not stored as a contact |
| Notification | One accepted request to inform a recipient — the promise |
| Rendered message | The finished subject and body after blanks are filled |
| Delivery attempt | One try at handing a rendered message to a sender |
| Outcome | What the sender said: accepted, refused, unreachable |
| History | The queryable record of notifications and their attempts |

### Business actions

Take ownership of a tenant · authenticate · configure a sender · prove a sender ·
write a template · grant machine access · revoke machine access · request a notification ·
accept a request · render · attempt delivery · record an outcome · retry · give up · inspect history.

### Business rules (observed, not yet formalised)

1. Nothing can be requested before a tenant has a working sender and a template.
2. Accepting a request is a promise: once accepted, the request must reach a terminal outcome.
3. A request never waits for the sender — acceptance and delivery are separate moments.
4. A refusal by the sender for a permanent reason must not be tried again; an unreachable sender must.
5. Giving up is a decision with a recorded reason, never silence.
6. A human may ask for another attempt after the system has given up.
7. Everything is read and written within one tenant; nothing crosses.
8. A revoked machine key stops working at once, but the notifications it already created remain.
9. A sending credential can be written and used, never read back.

### States and lifecycles

**Notification** — the promise made to the producer:

```
accepted ──▶ in progress ──▶ sent ──▶ (confirmed | bounced)
    │             │
    │             ├──▶ failed  (given up after retries, or permanently refused)
    │             └──▶ retried by a human ──▶ in progress
    └──▶ rejected (never accepted: unknown template, missing variable, bad address)
```

`rejected` happens synchronously and creates no promise. `confirmed` and `bounced` require the
sender to report back and are therefore out of MVP scope; `sent` is the MVP's success state and
means "the sending account took it", not "the human read it".

**Delivery attempt** — one try, immutable once finished:

```
started ──▶ succeeded
        └─▶ failed (transient → another attempt allowed)
                  (permanent → no further attempt)
```

**Sender** — `configured → verified → active → disabled`. A sender that has never been proven may be
used, but the administrator has not been shown that it works.

**Machine key** — `active → revoked`. No intermediate state.

**Template** — `draft → in use → withdrawn`. A withdrawn template cannot start new notifications;
notifications already accepted with it keep the wording they were rendered with.

## 2. Candidate domains

| Domain | Responsibility | Not its responsibility |
|--------|---------------|------------------------|
| **Identity & Access** | Tenants, administrators, sessions, machine keys, who may do what | What is sent, and to whom |
| **Sender Configuration** | The ways messages can leave: sender records, their secrets, proving them | Message content; when to send |
| **Message Content** | Templates, their variables, turning a template plus data into a finished message | Who is allowed to use a template; how it is sent |
| **Notification Intake** | Accepting or rejecting a request, the promise, idempotency | How the message is worded; how it leaves |
| **Delivery** | Attempts, retry policy, giving up, provider-specific behaviour | Whether the request was legitimate; the wording |
| **History & Audit** | The durable record and the questions asked of it | Making anything happen |

Relationships:

```
Identity & Access ─── owns ──▶ everything below (tenant boundary)

Notification Intake ──asks──▶ Message Content  (render this wording with this data)
        │                             ▲
        │                             │ uses the template that
        └──hands over──▶ Delivery ────┘
                             │
                             └──uses──▶ Sender Configuration (which account, which secret)

All of the above ──record into──▶ History & Audit
```

Note the dependency direction: Delivery knows about Sender Configuration but Sender Configuration
knows nothing about notifications; Message Content knows nothing about delivery at all. Intake is
the only domain a producer talks to.

## 3. Invariants

Always true, at every moment:

**Identity & Access**

- I1. Every stored record belongs to exactly one tenant.
- I2. No read or write ever resolves data belonging to another tenant, whatever identifier is supplied.
- I3. A revoked machine key authenticates nothing from the moment of revocation.
- I4. A sending secret can be written and used, never returned by any read.

**Intake**

- I5. An accepted notification is durably recorded before the caller is told it was accepted.
- I6. An accepted notification always reaches a terminal outcome — sent, failed or cancelled; it can
  never remain in progress indefinitely.
- I7. A notification references a template that existed and a sender that existed at the moment of
  acceptance.
- I8. A rejected request leaves no notification behind.

**Content**

- I9. Rendering with an unsupplied variable is a rejection, not an empty blank.
- I10. What was sent is reproducible: the notification keeps the wording it was rendered with, even
  if the template later changes.

**Delivery**

- I11. Every delivery attempt belongs to exactly one notification and one sender.
- I12. A finished attempt is never modified; another try creates another attempt.
- I13. A permanently refused notification is never attempted again automatically.
- I14. A failed notification carries a reason; failure without a reason is impossible.
- I15. The number of automatic attempts for one notification never exceeds the configured limit.
- I16. A human retry creates a new attempt; it never erases the earlier ones.

**History**

- I17. History is append-only: an outcome, once recorded, is never rewritten.
- I18. Every notification in history can be traced to the caller that created it.

## 4. Preliminary data ownership

| Data | Owning domain |
|------|--------------|
| Tenant, administrator account, session | Identity & Access |
| Machine key and its permissions | Identity & Access |
| Sender record, sending secret, verification result | Sender Configuration |
| Template, variable definitions, template versions | Message Content |
| Rendered subject and body | Message Content (produced), Notification Intake (kept with the promise) |
| Notification record, idempotency marker, recipient of that request | Notification Intake |
| Delivery attempt, provider reference, failure reason | Delivery |
| Retry policy and its limits | Delivery |
| Queryable history, audit of configuration changes | History & Audit |

Rendered content is the one shared item: Message Content produces it, but it is stored with the
notification so that what was sent stays reproducible (I10).

## 5. Ambiguous terms

Words that mean different things depending on who says them. Each needs one agreed meaning.

| Term | Meaning A | Meaning B | Risk if left unresolved |
|------|-----------|-----------|------------------------|
| **Notification** | The request the producer made | The email that arrived | Confuses the promise with the outcome; one request may produce several attempts |
| **Sent** | We handed it to the sending account | It reached the recipient's inbox | Reporting a success rate we cannot actually observe |
| **Delivered** | The provider confirmed acceptance downstream | The person read it | Promising a guarantee the MVP cannot make |
| **Failed** | This attempt failed | We gave up on the notification | An administrator retries the wrong thing |
| **Recipient** | An address on one request | A known person with preferences | Drifts toward owning a contact directory (a non-goal) |
| **Channel** | The kind of transport (email) | A configured account (this SMTP host) | Confusion once a tenant has two email accounts |
| **Template** | The reusable wording | The exact text that was sent | Editing a template appears to rewrite history |
| **Tenant** | A customer organisation | An application that sends | Decides whether one customer can separate its applications |
| **Retry** | Automatic re-attempt by the system | Deliberate re-send by a human | Retry limits and audit become meaningless |

Proposed working definitions, to be confirmed:

- **Notification** = the accepted request (the promise). The thing that arrives is a *delivery*.
- **Sent** = the sending account accepted the message. Nothing stronger is claimed in v1.
- **Delivered** = the provider reported downstream acceptance; unavailable in v1.
- **Failed** = the notification was given up on. A single unsuccessful try is a *failed attempt*.
- **Channel** = the transport kind (email). **Sender** = a configured account of that kind.
- **Tenant** = the unit of ownership and isolation; an application inside a tenant is identified by
  its machine key, not by a separate tenant.

## Open points

Questions this discovery raises that must be answered before the architecture is decided:

1. **Tenant granularity** — is one tenant one customer organisation (several producer applications
   share senders and templates), or one application? This decides whether isolation needs a second
   level below the tenant.
2. **Sender selection** — with one sender per tenant, delivery has no choice to make. If a tenant may
   have several, does the producer choose, does the template declare it, or is one marked default?
3. **Template ownership** — may a producer send wording of its own (inline content), or must every
   notification use an approved template? This decides whether Message Content is a gate or a helper.
4. **Meaning of success for the business** — is "the sending account accepted it" enough to report
   to a tenant, or is the product expected to claim inbox delivery (which requires provider feedback
   and moves work into v1)?
5. **Retry authority** — may a producer trigger a retry, or only a human administrator?
6. **History retention** — how long must rendered content and recipient addresses be kept, and who
   may read the body of a message after it was sent?
7. **Multi-channel shape** — when a second channel arrives, is one request allowed to fan out to
   several channels, or is a request always one channel? Answering now avoids reshaping the
   notification concept later.
