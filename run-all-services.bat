@echo off
setlocal

REM Run from repo root
pushd "%~dp0"

REM Optional: uncomment to skip build on each run
REM set NOBUILD=--no-build

start "TRC Hub" cmd /k "dotnet run %NOBUILD% --project hub-agent\src\TelegramRemoteControl.Hub\TelegramRemoteControl.Hub.csproj"
start "TRC BotService" cmd /k "dotnet run %NOBUILD% --project hub-agent\src\TelegramRemoteControl.BotService\TelegramRemoteControl.BotService.csproj"
start "TRC Agent" cmd /k "dotnet run %NOBUILD% --project hub-agent\src\TelegramRemoteControl.Agent\TelegramRemoteControl.Agent.csproj"

popd
endlocal
