# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

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

[Unreleased]: https://github.com/jerome-duchene/entra-secret-watcher/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/jerome-duchene/entra-secret-watcher/compare/v0.2.1...v0.3.0
