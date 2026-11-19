@echo off
setlocal
cd /d "%~dp0"
title AI-Simulation launcher

where dotnet >nul 2>&1
if errorlevel 1 (
    echo The .NET 8 SDK was not found. Install it with: winget install Microsoft.DotNet.SDK.8
    pause
    exit /b 1
)

if not exist "frontend\assets\maps\coldwar.json" (
    echo Map data is missing. Run once: pip install shapely, then python data\world\build_map.py
)

netstat -ano | findstr /R /C:":5080 .*LISTENING" >nul
if errorlevel 1 (
    echo Starting the simulation server...
    start "AI-Simulation API" /D "%~dp0simulation\Simulation.API" cmd /k dotnet run
) else (
    echo A server is already listening on port 5080. Reusing it.
)

echo Waiting for the server to answer...
powershell -NoProfile -Command "$h=@{'X-Access-Key'='dev-ground-truth-key'}; for($i=0;$i -lt 180;$i++){ try{ Invoke-WebRequest -UseBasicParsing -Headers $h -TimeoutSec 2 http://localhost:5080/api/simulation/status | Out-Null; exit 0 } catch { Start-Sleep -Seconds 1 } }; exit 1"
if errorlevel 1 (
    echo The server did not come up in time. Read the AI-Simulation API window for the error.
    pause
    exit /b 1
)

if /I "%~1"=="ai" (
    where python >nul 2>&1
    if errorlevel 1 (
        echo Python was not found, so the AI agent was not started.
    ) else (
        echo Starting the AI agent...
        start "AI-Simulation Agent" /D "%~dp0" cmd /k python -m ml.run_agent
    )
)

echo Opening http://localhost:5080
start "" http://localhost:5080
endlocal
