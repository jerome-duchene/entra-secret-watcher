# Claude Code — Prompt d'initialisation pour entra-secret-watcher

## Contexte du projet

Tu travailles sur **entra-secret-watcher**, un outil open-source (Apache 2.0) qui surveille l'expiration des client secrets et certificats des app registrations Microsoft Entra ID (Azure AD). C'est un projet communautaire hébergé sur GitHub.

## Architecture

- **.NET 10** — ASP.NET Core minimal API
- **Hangfire** (InMemory) — scheduling des scans + dashboard `/hangfire`
- **Microsoft Graph SDK** — scan des app registrations via `Application.Read.All`
- **Docker** — un container par tenant, configuration 100% via variables d'environnement
- **OpenTelemetry** — traces OTLP optionnelles

## Structure du projet

```
entra-secret-watcher/
├── Dockerfile                          # Multi-stage Alpine
├── docker-compose.yml                  # Exemple multi-tenant
├── README.md
├── LICENSE                             # Apache 2.0
└── src/
    ├── Program.cs                      # Entry point, health endpoint, Hangfire setup
    ├── appsettings.json
    ├── EntraSecretWatcher.csproj
    ├── Configuration/
    │   └── Options.cs                  # WatcherOptions, EntraOptions, NotificationOptions, OtelOptions
    ├── Models/
    │   └── Credentials.cs              # ExpiringCredential, ScanResult, enums
    ├── Services/
    │   ├── GraphCredentialScanner.cs    # ICredentialScanner — scan via Graph API avec pagination
    │   └── Notifications/
    │       ├── NotificationDispatcher.cs   # Dispatch vers tous les canaux activés
    │       ├── MessageBuilder.cs           # Génération plain text, HTML, Adaptive Cards
    │       ├── GotifyNotificationChannel.cs
    │       ├── EmailNotificationChannel.cs # Via Graph API (Mail.Send)
    │       └── TeamsNotificationChannel.cs # Via Incoming Webhook
    ├── Jobs/
    │   └── CredentialScanJob.cs        # Job Hangfire orchestrant scan + notification
    └── Extensions/
        └── ServiceCollectionExtensions.cs  # DI registration
```

## Canaux de notification

1. **Gotify** — push notification self-hosted, POST simple vers `/message`
2. **Email** — via Microsoft Graph API (`Mail.Send`), nécessite une mailbox ou shared mailbox
3. **Teams** — via Incoming Webhook avec Adaptive Cards (pas de permission Graph nécessaire)

## Patterns et conventions

- Configuration typée via `IOptions<T>` avec binding depuis variables d'environnement (préfixe `__` .NET)
- Injection de dépendances : `INotificationChannel` enregistré comme collection, dispatch par `INotificationDispatcher`
- Structured logging avec `ILogger<T>`
- `ActivitySource` pour les traces OpenTelemetry sur le scan et les jobs
- Le scanner utilise `PageIterator` du Graph SDK pour paginer les résultats
- Dry-run mode : log sans notifier
- Grouped report : une seule notification résumée par scan

## Conventions de code

- C# moderne (file-scoped namespaces, records, pattern matching, raw string literals)
- Nullable reference types activés
- Nommage : PascalCase pour les propriétés publiques, _camelCase pour les champs privés
- Async/await partout avec CancellationToken propagé
- Pas de magic strings : constantes pour les noms de sections de config

## Ce qui reste à faire / améliorations possibles

- [ ] Ajouter des tests unitaires (xUnit + NSubstitute ou Moq)
- [ ] Valider la configuration au démarrage (vérifier qu'au moins un canal de notification est activé, que les options Entra sont renseignées)
- [ ] Ajouter un endpoint `/scan` pour déclencher un scan manuellement via HTTP
- [ ] Séparer le MessageBuilder en stratégies par canal si la complexité augmente
- [ ] Ajouter un canal de notification générique Webhook (POST JSON vers une URL arbitraire)
- [ ] GitHub Actions CI/CD : build, test, push image Docker vers GitHub Container Registry
- [ ] Gérer le cas où le scan échoue (notification d'erreur sur les canaux configurés)
- [ ] Support des certificats pour l'authentification (en plus des client secrets)
- [ ] Versionning sémantique automatique