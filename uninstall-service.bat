@echo off
:: Run as Administrator

echo ===========================================
echo   Uninstall TelegramRemoteControl Service
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

:: Check if service exists
sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Service not installed
    pause
    exit /b 0
)

:: Stop service
echo Stopping service...
sc stop %SERVICE_NAME% >nul 2>&1
timeout /t 3 >nul

:: Delete service
echo Removing service...
sc delete %SERVICE_NAME%

echo.
echo ===========================================
echo   Done!
echo ===========================================
echo.
echo Service removed successfully.
echo.
pause
