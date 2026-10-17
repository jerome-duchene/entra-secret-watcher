# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.5.0] - 2026-10-17

### Added
- **`POST /scan`** to trigger a scan on demand, protected by a secret sent in the `X-Scan-Token` header.
  Enabled only when `Watcher__ManualTriggerSecret` (at least 16 characters) is set. The Hangfire
  dashboard stays read-only.
- **`Watcher__RunOnStartup`** to also run a scan when the application starts, so metrics are
  available right after a restart instead of waiting for the next cron.

## [0.4.0] - 2026-10-17

### Added
- **OpenTelemetry OTLP export** (`OpenTelemetry__*`): traces, metrics and logs pushed to Grafana
  Alloy / an OpenTelemetry Collector (gRPC or HTTP/protobuf), with `service.name` and
  `tenant.name` resource attributes. Health and Prometheus probes are not traced.
- `key_id` label on `esw_credential_days_left`, the same unique identifier as in the webhook payload.
- `tenant_name` and `tenant_id` labels on **every** `esw_*` series (both on `/metrics` and over OTLP),
  to tell apart the containers when one is deployed per tenant. OTLP resource attributes also
  include `service.instance.id` (tenant id) and `tenant.id`.

- **Grafana dashboard** in [`grafana/`](grafana/): overview, one row per tenant, credentials to renew,
  plus logs (Loki) and traces (Tempo) rows. One dashboard covers all tenants, with a *Tenant* variable.

### Changed
- The `esw_*` metrics are also published as `System.Diagnostics.Metrics` instruments for the OTLP
  export, with the same names as on `/metrics`. Both come from the same snapshot of the last scan.
- No `esw_*` series is published before the first scan (instead of misleading zeros), so a
  `time() - esw_last_scan_timestamp_seconds` alert does not fire right after a restart.

### Fixed
- The 0.3.0 documentation and compose example described an OpenTelemetry setting that had no
  implementation. It is now implemented.

## [0.3.0] - 2026-10-17

### Added
- **Prometheus metrics** exposed on `GET /metrics`: days left per credential
  (`esw_credential_days_left`), expired / expiring-soon counts, apps scanned, last scan
  timestamp and success flag. Values persist between scans.
- **Generic webhook notification channel** (`Notification__Webhook__*`): one JSON POST per
  expiring credential with an optional shared-secret header, to plug in Jira Automation, n8n,
  Power Automate…
- **`keyId`** (Entra key identifier) on each credential, sent in the webhook payload, as a stable
  unique identifier (names can be empty or duplicated).

### Changed
- README: new sections for metrics (with Prometheus scrape config and alert rules), the webhook
  channel and the release process.

[Unreleased]: https://github.com/jerome-duchene/entra-secret-watcher/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/jerome-duchene/entra-secret-watcher/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/jerome-duchene/entra-secret-watcher/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/jerome-duchene/entra-secret-watcher/compare/v0.2.1...v0.3.0
