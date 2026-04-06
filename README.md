# Fanfic Downloader

![.NET](https://img.shields.io/badge/.NET-10-blue)
![Status](https://img.shields.io/badge/status-active-success)

Live demo: https://fanficdownloader.com


Fanfic Downloader is a web service that downloads fanfics from supported websites and generates **EPUB** or **TXT** files.

The service provides:

- Web interface
- REST API
- Telegram bot integration

It is designed as a small production-ready service with a queue-based architecture to safely process downloads without overloading the server.

---

# Features

- Download fanfics as **EPUB** or **TXT**
- Web UI for quick downloads
- REST API for automation
- Telegram bot integration
- Queue-based download system
- Configurable concurrency
- Source-specific parsers
- Proxy rotation for ficbook.net requests
- Optional Cloudflare bypass via FlareSolverr
- PostgreSQL-backed cache metadata storage
- R2 object storage for cached TXT/EPUB files
- Cache-first download flow (reuse previously generated files)
- Docker deployment
- Nginx reverse proxy
- HTTPS via Let's Encrypt
- Health endpoint for monitoring

---

# Supported sources

Currently supported fanfiction sites:

- ficbook.net *(proxy-first, with FlareSolverr fallback)*  
- snapetales.com  
- fanfiction.net *(via FlareSolverr)*  
- walkingtheplank.org  

Additional sources can be added by implementing new `IFanficSource` implementations.

---

# Architecture

Typical production architecture:

```
Internet
↓
Nginx (HTTPS / reverse proxy)
↓
ASP.NET Core (Kestrel)
↓
FanficDownloader service
↓
Download queue
↓
Fanfic parsers
↓
EPUB/TXT generation
```

Downloads are processed through an internal queue to:

- limit concurrency
- prevent server overload
- ensure stable performance

---

# Tech stack

Backend

- .NET 10
- ASP.NET Core
- Kestrel web server
- Background services

Infrastructure

- Docker
- Nginx reverse proxy
- Let's Encrypt HTTPS
- FlareSolverr (optional)

---

# Project structure

```
FanficDownloader

FanficDownloader.Web
    Controllers
        DownloadController
        QueueController
    Services
        DownloadQueueService
        TelegramBotBackgroundService
    Pages
        Index.cshtml
    Program.cs
    DependencyInjection.cs
    wwwroot

FanficDownloader.Application
    Services
        FanficDownloadService
        FanficCacheService
        ImageDownloadService
    Configuration
        DownloadSettings
        FlareSolverrSettings
    Security
        UrlValidator
    Models
        DownloadFileResult

FanficDownloader.Core
    Sources
        FicbookSource
        FanfictionNetSource
        SnapetalesSource
        WalkingThePlankSource
        SourceManager
    Parsers
        FicbookParser
        FanfictionNetParser
        SnapetalesParser
        WalkingThePlankParser
    Formatting
        FanficEpubFormatter
        FanficTxtFormatter
    Clients
        FlareSolverrClient
    Services
        R2StorageService
        ProxyService
        ProxyState
    Models
        Fanfic
        Chapter
        DownloadResult
        ImageInfo
        FanficCacheEntry

FanficDownloader.Bot
    Services
        FanficService
    Formatting
        FanficTelegramFormatter

FanficDownloader.Tests
    Parser tests
    Formatter tests
    Source tests
```

---

# Quick start

## 1. Clone repository

```bash
git clone https://github.com/YOUR_USERNAME/FanficDownloader.git
cd FanficDownloader
```

---

## 2. Configure

Edit:

```
FanficDownloader.Web/appsettings.json
```

Example configuration:

```json
{
  "Download": {
    "MaxConcurrentDownloads": 2
  },
  "FlareSolverr": {
    "Url": "http://localhost:8191"
  }
}
```

Optional: set Telegram bot token

```
TG_BOT_TOKEN=<your_token>
```

---

## 3. Run the application

```bash
dotnet restore
dotnet run --project FanficDownloader.Web
```

Open in browser:

```
http://localhost:5000
```

---

## 4. Run FlareSolverr (optional)

Cloudflare-protected sources use FlareSolverr (fanfiction.net and ficbook.net fallback path).

Start FlareSolverr and ensure the URL matches the configuration.

Example:

```
http://localhost:8191
```

---

# API endpoints

| Endpoint | Description |
|--------|--------|
| POST /download/info | Fetch fanfic metadata |
| POST /download/txt | Download TXT |
| POST /download/epub | Download EPUB |
| GET /queue/position | Queue position |
| GET /health | Health check |

---

# Health endpoint

The service exposes a simple health endpoint.

```
GET /health
```

Example:

```
https://fanficdownloader.com/health
```

Response:

```
OK
```

This endpoint can be used by:

- monitoring systems
- uptime checks
- container health checks

---

# Queue system

All downloads are processed through an internal queue.

This ensures that:

- only a limited number of downloads run simultaneously
- the server cannot be overloaded
- large downloads do not block the system

Concurrency is controlled via:

```
Download:MaxConcurrentDownloads
```

---

# Cache system (PostgreSQL + R2)

The service uses a cache layer to avoid rebuilding the same files repeatedly.

How it works:

1. On TXT/EPUB request, the service checks PostgreSQL table `fanfic_cache` by:
   - source URL
   - output format (`txt` or `epub`)
2. If a cache record exists, the file is downloaded from object storage (R2) and returned immediately.
3. If no cache record exists, the fanfic is downloaded and generated normally.
4. The generated file is uploaded to R2, and metadata is saved to PostgreSQL.

Cached metadata includes:

- `url`
- `title`
- `object_key`
- `created_at`
- `format`

This approach reduces repeated parsing/formatting work and improves response time for repeated downloads.

---

# PostgreSQL

PostgreSQL is used for cache metadata persistence (`fanfic_cache` table).

In Docker Compose, PostgreSQL is started as:

- service: `postgres`
- image: `postgres:16`
- database: `fanficdownloader`

Connection string is configured in `appsettings.json`:

```json
"ConnectionStrings": {
  "Postgres": "Host=localhost;Port=5432;Database=fanficdownloader;Username=postgres;Password=..."
}
```

When running in containers, ensure the host and credentials match your compose/network setup.

---

# Telegram bot

The Telegram bot runs as a background service inside the web host.

If `TG_BOT_TOKEN` is set, the bot automatically starts when the application launches.

---

# Deployment

Typical production deployment:

1. Docker container running the application  
2. PostgreSQL container for cache metadata  
3. (Optional) FlareSolverr container for Cloudflare-protected sources  
4. Nginx reverse proxy  
5. HTTPS via Let's Encrypt  
6. Optional Cloudflare DNS  

Deployment workflow:

```
git push
↓
ssh server
↓
git pull
↓
docker compose up -d --build
```

---

# Running tests

```
dotnet test
```

---

# Contributing

Contributions are welcome.

If you'd like to contribute:

1. Fork the repository
2. Create a feature branch

```
git checkout -b feature/my-feature
```

3. Implement your changes
4. Add tests if applicable
5. Commit and push

```
git commit -m "Add new feature"
git push
```

6. Open a Pull Request

Please ensure that:

- the project builds successfully
- tests pass
- new features include tests when possible

---

## UI contributions welcome

The current web interface is intentionally minimal.

If you'd like to contribute improvements to the UI or create a better design, contributions are very welcome.

Frontend improvements, UI redesigns, and UX enhancements would be greatly appreciated.

# Future improvements

Possible future features:

- rate limiting
- download size limits
- monitoring (CPU / memory)
- additional fanfiction sources
- UI improvements
