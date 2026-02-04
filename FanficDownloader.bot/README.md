**Live Telegram bot:** https://t.me/FanficDownloaderBot

# 📚 FanficDownloader Telegram Bot

FanficDownloader is a Telegram bot that allows users to download fanfiction from multiple websites and receive it as TXT or EPUB files directly in Telegram.

The bot parses full stories with chapters and metadata, supports multiple fanfiction platforms, and generates ready-to-read files for offline use.

The project is deployed on a Linux VPS and runs 24/7 as a systemd-managed service.

---

## 🚀 Features

- Download fanfiction via Telegram  
- Supports multiple platforms:
  - Ficbook  
  - Fanfiction.net (via FlareSolverr + Docker)  
  - Snapetales  
  - WalkingThePlank  
- Automatic language detection (English / Russian)  
- Output formats:
  - 📄 TXT  
  - 📚 EPUB  
- Interactive Telegram UI with inline buttons  
- Real-time progress messages  
- 24/7 uptime on a Linux VPS  

---

## 🧱 System Architecture

This project is built as a modular, extensible backend system:

```
Telegram users
      ↓
Telegram API
      ↓
.NET Telegram Bot
      ↓
Source Manager
      ↓
Ficbook / Snapetales / WalkingThePlankm / Fanfiction.net (via FlareSolverr + Docker)
      ↓
Fanfic Models → Formatters → TXT / EPUB
```

Fanfiction.net is accessed through a separate **FlareSolverr** service running in Docker to bypass Cloudflare protection.

---

## 🛠 Tech Stack

- **C#**
- **.NET 8**
- **Telegram Bot API**
- **HTML parsing & web scraping**
- **EPUB and TXT file generation**
- **Docker**
- **FlareSolverr (Cloudflare bypass)**
- **Linux (Ubuntu VPS)**
- **systemd (24/7 service management)**
- **Environment variables for secrets**
- **Git & GitHub**

---

## 🔐 Secrets & Configuration

Telegram tokens are never stored in source code.

They are provided via environment variables:

```
/etc/fanficbot.env
TG_BOT_TOKEN=YOUR_TELEGRAM_BOT_TOKEN
```

The application reads it using:

```csharp
Environment.GetEnvironmentVariable("TG_BOT_TOKEN")
```

---

## ⚙️ Running Locally

```bash
git clone https://github.com/nornan01/FanficDownloader.git
cd FanficDownloader
dotnet restore
dotnet run
```

Set the token:

Linux / macOS:
```bash
export TG_BOT_TOKEN=your_token_here
```

Windows:
```powershell
setx TG_BOT_TOKEN "your_token_here"
```

---

## 🌍 Production Deployment

The bot runs on an Ubuntu VPS and is managed by systemd.

FlareSolverr runs in Docker as a separate service.

Deployment workflow:

```bash
git pull
dotnet publish -c Release -o /opt/fanficbot
systemctl restart fanficbot
```

FlareSolverr:

```bash
docker run -d --name flaresolverr -p 8191:8191 ghcr.io/flaresolverr/flaresolverr:latest
```

---

## System Design Highlights

- Multi-source architecture using IFanficSource  
- Dockerized FlareSolverr for Cloudflare-protected websites  
- Linux systemd service for 24/7 uptime  
- Environment-based secret management  


---

## 📌 Planned Features

- GitHub Actions CI/CD  
- Unit and integration tests  
- More fanfiction sources  
- Improved EPUB styling  
- User profiles and download history  
    