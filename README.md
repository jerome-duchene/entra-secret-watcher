# 🔐 Entra Secret Watcher

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-ready-blue)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue)](LICENSE)
[![CI](https://github.com/YOUR_USERNAME/entra-secret-watcher/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR_USERNAME/entra-secret-watcher/actions/workflows/ci.yml)

A lightweight, self-hosted Docker container that monitors **Microsoft Entra ID** (Azure AD) app registration **secrets and certificates** for expiration, and sends proactive notifications before they expire.

**One container per tenant** — deploy as many as you need, configure everything via environment variables.

## ✨ Features

- 🔍 Scans all app registrations for expiring **client secrets** and **certificates**
- 📊 Grouped report — single notification summarizing all expiring credentials
- 🔔 Multiple notification channels:
  - **Gotify** — self-hosted push notifications
  - **Email** — via Microsoft Graph API (no SMTP needed)
  - **Microsoft Teams** — via Graph API with Adaptive Cards
- ⏰ Scheduled scanning via **Hangfire** with built-in dashboard
- 🧪 **Dry-run mode** — test without sending notifications
- 📡 **OpenTelemetry** traces for observability
- 🐳 Lightweight Alpine-based Docker image (~50MB)
- 💚 Built-in health check endpoint

## 📋 Prerequisites

### Entra ID App Registration

Create an app registration in each tenant you want to monitor:

1. Go to **Entra ID** → **App registrations** → **New registration**
2. Name: `entra-secret-watcher` (or your preference)
3. Supported account types: **Single tenant**
4. No redirect URI needed

#### Required API Permissions

| Notification Channel | Permission          | Type        |
|---------------------|---------------------|-------------|
| **All**             | `Application.Read.All` | Application |
| **Email**           | `Mail.Send`            | Application |

> **Teams** uses an Incoming Webhook — no additional API permission required.

> **Grant admin consent** after adding the permissions.

#### Create a Client Secret

Add a client secret (or certificate) and note the value — you'll need it for configuration.

> 💡 **Pro tip**: This app registration's secret will also be monitored by the watcher itself! The watcher watches its own credentials.

## 🚀 Quick Start

### 1. Pull the image (or build locally)

```bash
# Pull from GitHub Container Registry
docker pull ghcr.io/YOUR_USERNAME/entra-secret-watcher:latest

# Or build locally
docker build -t entra-secret-watcher:latest .
```

### 2. Run with Docker

```bash
docker run -d \
  --name esw-contoso \
  --restart unless-stopped \
  -p 8080:8080 \
  -e Entra__TenantId="your-tenant-id" \
  -e Entra__ClientId="your-client-id" \
  -e Entra__ClientSecret="your-secret" \
  -e Entra__TenantName="Contoso" \
  -e Notification__Gotify__Enabled="true" \
  -e Notification__Gotify__Url="https://gotify.example.com" \
  -e Notification__Gotify__Token="your-gotify-token" \
  entra-secret-watcher:latest
```

### 3. Verify

```bash
# Health check
curl http://localhost:8080/health

# Hangfire dashboard
open http://localhost:8080/hangfire
```

## ⚙️ Configuration

All settings can be configured via **environment variables** using the .NET `__` (double underscore) convention.

### Entra ID Connection

| Variable | Description | Required |
|----------|-------------|----------|
| `Entra__TenantId` | Azure AD tenant ID | ✅ |
| `Entra__ClientId` | App registration client ID | ✅ |
| `Entra__ClientSecret` | App registration client secret | ✅ |
| `Entra__TenantName` | Friendly name (used in notifications) | No (default: `Default`) |

### Watcher Settings

| Variable | Description | Default |
|----------|-------------|---------|
| `Watcher__ThresholdDays` | Days before expiration to alert | `30` |
| `Watcher__CronSchedule` | Hangfire cron expression | `0 8 * * *` (daily 8AM) |
| `Watcher__DryRun` | Log results without sending notifications | `false` |
| `Watcher__GroupedReport` | Send one grouped notification | `true` |

### Notification — Gotify

| Variable | Description |
|----------|-------------|
| `Notification__Gotify__Enabled` | `true` / `false` |
| `Notification__Gotify__Url` | Gotify server URL |
| `Notification__Gotify__Token` | Gotify application token |

### Notification — Email (via Graph API)

| Variable | Description |
|----------|-------------|
| `Notification__Email__Enabled` | `true` / `false` |
| `Notification__Email__From` | Sender email (must be a valid mailbox or shared mailbox) |
| `Notification__Email__To` | Comma-separated recipient addresses |

> Requires `Mail.Send` application permission on the app registration.

### Notification — Teams (via Incoming Webhook)

| Variable | Description |
|----------|-------------|
| `Notification__Teams__Enabled` | `true` / `false` |
| `Notification__Teams__WebhookUrl` | Incoming Webhook URL from the Teams channel connector |

> No Graph API permission required. Create the webhook in Teams under **Channel → Connectors → Incoming Webhook**.

### OpenTelemetry (optional)

| Variable | Description | Default |
|----------|-------------|---------|
| `OpenTelemetry__Enabled` | Enable OTLP tracing | `false` |
| `OpenTelemetry__Endpoint` | OTLP collector endpoint | `http://localhost:4317` |

## 🐳 Multi-Tenant Deployment

Use `docker-compose.yml` to deploy one container per tenant:

```yaml
services:
  watcher-contoso:
    image: entra-secret-watcher:latest
    environment:
      Entra__TenantId: "tenant-a-id"
      Entra__TenantName: "Contoso"
      # ... other config

  watcher-fabrikam:
    image: entra-secret-watcher:latest
    environment:
      Entra__TenantId: "tenant-b-id"
      Entra__TenantName: "Fabrikam"
      # ... other config
```

See the included `docker-compose.yml` for a complete example.

## 🔍 Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /health` | Health check (returns tenant name and timestamp) |
| `GET /hangfire` | Hangfire dashboard (read-only) |

## 🧪 Dry-Run Mode

Test the scanner without sending any notifications:

```bash
-e Watcher__DryRun="true"
```

All detected credentials will be logged to stdout — useful for validating permissions and connectivity before going live.

## 📡 Observability

When OpenTelemetry is enabled, the application emits traces for:

- `EntraSecretWatcher.Scanner` — credential scanning operations
- `EntraSecretWatcher.Jobs` — Hangfire job execution
- HTTP client calls to Graph API and notification endpoints

Compatible with any OTLP collector (SigNoz, Jaeger, Grafana Tempo, etc.).

## 🏗️ Architecture

```
┌─────────────────────────────────────────────┐
│          entra-secret-watcher               │
│                                             │
│  ┌───────────┐    ┌──────────────────────┐  │
│  │ Hangfire   │───▶│ CredentialScanJob    │  │
│  │ Scheduler  │    │                      │  │
│  └───────────┘    │  ┌────────────────┐  │  │
│                   │  │ Graph API Scan │  │  │
│  ┌───────────┐    │  └───────┬────────┘  │  │
│  │ /health   │    │          │           │  │
│  │ /hangfire │    │  ┌───────▼────────┐  │  │
│  └───────────┘    │  │ Notification   │  │  │
│                   │  │ Dispatcher     │  │  │
│                   │  └───┬───┬───┬────┘  │  │
│                   └──────┼───┼───┼───────┘  │
│                          │   │   │          │
└──────────────────────────┼───┼───┼──────────┘
                           │   │   │
                    ┌──────┘   │   └──────┐
                    ▼          ▼          ▼
                 Gotify    Email      Teams
                          (Graph)   (Graph)
```

## 🧪 Development

### Running tests

```bash
# Unit tests only (fast, no credentials needed)
dotnet test --filter "Category!=Integration"

# Integration tests (requires real credentials — see below)
dotnet test --filter "Category=Integration"
```

Integration tests connect to real external services (Entra ID, Gotify, Teams, email). Configure
them via [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "Entra:TenantId"                "<guid>"   --project tests/EntraSecretWatcher.Tests/
dotnet user-secrets set "Entra:ClientId"                "<guid>"   --project tests/EntraSecretWatcher.Tests/
dotnet user-secrets set "Entra:ClientSecret"            "<secret>" --project tests/EntraSecretWatcher.Tests/
dotnet user-secrets set "Notification:Gotify:Url"       "https://…" --project tests/EntraSecretWatcher.Tests/
dotnet user-secrets set "Notification:Gotify:Token"     "<token>"  --project tests/EntraSecretWatcher.Tests/
dotnet user-secrets set "Notification:Email:From"       "x@dom.com" --project tests/EntraSecretWatcher.Tests/
dotnet user-secrets set "Notification:Email:To"         "y@dom.com" --project tests/EntraSecretWatcher.Tests/
dotnet user-secrets set "Notification:Teams:WebhookUrl" "https://…" --project tests/EntraSecretWatcher.Tests/
```

Integration tests without configured credentials are automatically **skipped** (not failed).

### CI/CD

The project uses GitHub Actions (`.github/workflows/ci.yml`):

- **Every push / PR** → unit tests run automatically
- **On `v*.*.*` tag** → unit tests + multi-platform Docker image (`linux/amd64`, `linux/arm64`) published to `ghcr.io`

To publish a release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## 📄 License

Apache 2.0 — see [LICENSE](LICENSE).

## 🤝 Contributing

Contributions welcome! Please open an issue or submit a pull request.
