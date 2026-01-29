@echo off
:: Run as Administrator

echo ===========================================
echo   Install TelegramRemoteControl Service
echo ===========================================
echo.

:: Check admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Run as Administrator!
    pause
    exit /b 1
)

set SERVICE_NAME=TelegramRemoteControl
set SERVICE_PATH=%~dp0TelegramRemoteControl.exe

:: Check if exe exists
if not exist "%SERVICE_PATH%" (
    echo [ERROR] TelegramRemoteControl.exe not found!
    echo Place this script next to TelegramRemoteControl.exe
    pause
    exit /b 1
)

:: Stop and delete existing service
sc stop %SERVICE_NAME% >nul 2>&1
sc delete %SERVICE_NAME% >nul 2>&1
timeout /t 2 >nul

:: Create service
echo Creating service...
sc create %SERVICE_NAME% binPath= "\"%SERVICE_PATH%\"" start= auto DisplayName= "Telegram Remote Control"
sc description %SERVICE_NAME% "Telegram bot for remote PC control"
sc failure %SERVICE_NAME% reset= 86400 actions= restart/60000/restart/60000/restart/60000

:: Start service
echo Starting service...
sc start %SERVICE_NAME%

echo.
echo ===========================================
echo   Done!
echo ===========================================
echo.
sc query %SERVICE_NAME%
echo.
echo Service installed and started.
echo Logs: Event Viewer - Applications and Services
echo.
pause
