@echo off
echo ===========================================
echo   Build TelegramRemoteControl
echo ===========================================
echo.

set SERVICE_NAME=TelegramRemoteControl

:: Check admin rights (needed for service stop/start)
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Run as Administrator!
    pause
    exit /b 1
)

:: Stop service if running
sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo Stopping service...
    sc stop %SERVICE_NAME% >nul 2>&1
    :: Wait until the service is fully stopped
    :wait_stop
    sc query %SERVICE_NAME% | find "STOPPED" >nul 2>&1
    if %errorlevel% neq 0 (
        timeout /t 1 /nobreak >nul
        goto wait_stop
    )
    echo Service stopped.
    echo.
)

:: Build (use the directory where this script lives as project root)
set SCRIPT_DIR=%~dp0
dotnet publish "%SCRIPT_DIR%TelegramRemoteControl.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%SCRIPT_DIR%publish"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed
    echo Restarting service...
    sc start %SERVICE_NAME% >nul 2>&1
    pause
    exit /b 1
)

:: Cleanup
del /q "%SCRIPT_DIR%publish\*.pdb" 2>nul

:: Copy service scripts
copy /y "%SCRIPT_DIR%install-service.bat" "%SCRIPT_DIR%publish\" >nul
copy /y "%SCRIPT_DIR%uninstall-service.bat" "%SCRIPT_DIR%publish\" >nul

:: Start service back
echo.
echo Starting service...
sc start %SERVICE_NAME% >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Failed to start service
    pause
    exit /b 1
)

:: Wait until running
:wait_start
sc query %SERVICE_NAME% | find "RUNNING" >nul 2>&1
if %errorlevel% neq 0 (
    timeout /t 1 /nobreak >nul
    goto wait_start
)

echo Service started.

echo.
echo ===========================================
echo   Done!
echo ===========================================
echo.
echo Output folder: %SCRIPT_DIR%publish\
echo.
echo Contents:
dir /b "%SCRIPT_DIR%publish\"
echo.
pause
