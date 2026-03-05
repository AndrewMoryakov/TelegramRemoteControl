@echo off
setlocal

set SERVICE_NAME=TRCAgent
set DISPLAY_NAME=TRC Agent
set EXE_NAME=TelegramRemoteControl.Agent.exe

:: --- UAC elevation ---
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process -Verb RunAs -FilePath '%~f0' -ArgumentList '%~dp0'"
    exit /b
)

:: --- Resolve paths ---
set "SCRIPT_DIR=%~dp0"
set "EXE_PATH=%SCRIPT_DIR%%EXE_NAME%"

if not exist "%EXE_PATH%" (
    echo ERROR: %EXE_NAME% not found in %SCRIPT_DIR%
    echo Make sure install-service.bat is in the same folder as the published agent.
    pause
    exit /b 1
)

:: --- Check pairing ---
findstr /c:"AgentToken" "%SCRIPT_DIR%appsettings.json" >nul 2>&1
if %errorlevel% neq 0 (
    echo WARNING: AgentToken not found in appsettings.json.
    echo Please run pairing first:
    echo   %EXE_NAME% --hub ^<url^> --pair ^<code^> --name ^<name^>
    echo.
    set /p CONTINUE="Continue anyway? (y/N): "
    if /i not "%CONTINUE%"=="y" exit /b 1
)

:: --- Remove existing service if present ---
sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo Stopping existing %SERVICE_NAME% service...
    sc stop %SERVICE_NAME% >nul 2>&1
    timeout /t 3 /nobreak >nul
    echo Removing existing %SERVICE_NAME% service...
    sc delete %SERVICE_NAME%
    timeout /t 2 /nobreak >nul
)

:: --- Create service ---
echo Installing %SERVICE_NAME% service...
sc create %SERVICE_NAME% binPath= "\"%EXE_PATH%\"" start= auto obj= LocalSystem DisplayName= "%DISPLAY_NAME%"
if %errorlevel% neq 0 (
    echo ERROR: Failed to create service.
    pause
    exit /b 1
)

:: --- Configure failure recovery: 5s / 10s / 30s ---
sc failure %SERVICE_NAME% reset= 86400 actions= restart/5000/restart/10000/restart/30000

:: --- Start service ---
echo Starting %SERVICE_NAME% service...
sc start %SERVICE_NAME%
if %errorlevel% neq 0 (
    echo WARNING: Service created but failed to start. Check Event Viewer for details.
    pause
    exit /b 1
)

echo.
echo %SERVICE_NAME% service installed and started successfully.
echo   Startup type: Automatic
echo   Account:      LocalSystem
echo   Logs:         Event Viewer ^> Windows Logs ^> Application
echo.
pause
