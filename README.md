# 🔐 Entra Secret Watcher

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-ready-blue)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

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
| **Teams**           | `ChannelMessage.Send`  | Application |

> **Grant admin consent** after adding the permissions.

#### Create a Client Secret

Add a client secret (or certificate) and note the value — you'll need it for configuration.

> 💡 **Pro tip**: This app registration's secret will also be monitored by the watcher itself! The watcher watches its own credentials.

## 🚀 Quick Start

### 1. Build the image

```bash
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

### Notification — Teams (via Graph API)

| Variable | Description |
|----------|-------------|
| `Notification__Teams__Enabled` | `true` / `false` |
| `Notification__Teams__TeamId` | Microsoft Teams team ID |
| `Notification__Teams__ChannelId` | Teams channel ID |

> Requires `ChannelMessage.Send` application permission on the app registration.

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

## 📄 License

MIT — see [LICENSE](LICENSE).

## 🤝 Contributing

Contributions welcome! Please open an issue or submit a pull request.
