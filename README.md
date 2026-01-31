# Telepilot Hub

Self-hosted Telegram bot + hub + agent for remote control and monitoring.

## Components
- Hub: ASP.NET Core API + SignalR for agent connections and state.
- BotService: Telegram bot frontend that sends commands to the hub.
- Agent: runs on user PCs and executes commands.

## Features
- Device pairing via one-time code
- Command execution (status, processes, services, windows, files)
- File browser, preview, download
- Device status notifications (opt-in)
- User approvals (/approve, /deny)

## Quick start (server, Docker)
1) Copy `.env.example` to `.env` and fill values.
2) Run:
   ```
   docker compose up -d --build
   ```
3) Hub will be available at `http://<server>:HUB_PORT`.

## Quick start (local dev)
```
dotnet run --project hub-agent/src/TelegramRemoteControl.Hub/TelegramRemoteControl.Hub.csproj
dotnet run --project hub-agent/src/TelegramRemoteControl.BotService/TelegramRemoteControl.BotService.csproj
dotnet run --project hub-agent/src/TelegramRemoteControl.Agent/TelegramRemoteControl.Agent.csproj -- --setup
```

## Docs
- User guide: `docs/getting_started.md`
- Docker deploy: `docs/deploy_docker.md`
- VPS + Caddy: `deploy/vps-caddy/GETTING_STARTED.md`

