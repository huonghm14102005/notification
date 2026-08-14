# notification-server (notify-api)

A standalone, multi-tenant notification service. Applications hand it a message for a person; the
service renders it from a tenant-owned template and delivers it through the tenant's configured
sender. Delivery is asynchronous, retried and auditable.

Email is the only channel in the first version.

## Status

Definition stage — no implementation yet.

## Documentation

- [Product Brief](docs/PRODUCT.md) — problem, users, value, metrics, constraints, assumptions,
  risks, non-goals.
- [MVP Definition](docs/MVP.md) — the end-to-end journey, Must/Should/Could/Not-now scope, and the
  criteria for calling the MVP complete.

## Decisions taken

- Standalone service, not a module of the existing CDN/Media service.
- Built from scratch: its own datastore, its own tenancy and credentials; no reuse of the CDN
  service's tenants, users or API keys.
- Email first; other channels are out of scope for v1 but must not require a rewrite.
