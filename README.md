# Fanfic Downloader

Download fanfics as EPUB or TXT via a web UI, API, or Telegram bot.

## Features

- Web UI for quick downloads (EPUB/TXT)
- REST API endpoints
- Telegram bot integration
- Source-specific parsers
- Queue with configurable concurrency
- Optional FlareSolverr support for Cloudflare-protected sources

## Supported sources

- ficbook.net
- snapetales.com
- fanfiction.net (via FlareSolverr)
- walkingtheplank.org

## Tech stack

- .NET 10
- ASP.NET Core (Web + API)
- Background services

## Project structure

- `FanficDownloader.Web` — web UI, API, hosted services
- `FanficDownloader.Application` — application services, configuration
- `FanficDownloader.Core` — parsers, sources, formatting
- `FanficDownloader.Bot` — Telegram bot service
- `Fanficdownloader.tests` — unit tests

## Quick start

### 1) Configure

Edit `FanficDownloader.Web/appsettings.json`:

```json
{
  "Download": {
    "MaxConcurrentDownloads": 3
  },
  "FlareSolverr": {
    "Url": "http://localhost:8191"
  }
}
```

Set the Telegram token (optional, for the bot):

- `TG_BOT_TOKEN` environment variable

### 2) Run the web app

```bash
dotnet restore
dotnet run --project FanficDownloader.Web
```

Open the web UI at `http://localhost:5000` (or the URL printed by `dotnet run`).

### 3) (Optional) Run FlareSolverr

Fanfiction.net requests go through FlareSolverr. Start it and ensure the URL
matches `FlareSolverr:Url` in `appsettings.json`.

## API endpoints

- `POST /download/info` — metadata preview
- `POST /download/txt` — download TXT
- `POST /download/epub` — download EPUB
- `GET /queue/position` — current queue length

## Telegram bot

The bot is hosted inside the Web project as a background service. If you set
`TG_BOT_TOKEN`, it will start automatically when the web app runs.

## Tests

```bash
dotnet test
```

## Notes

- The queue concurrency is controlled via `Download:MaxConcurrentDownloads`.
- Temporary files are created during image downloads and EPUB builds.
