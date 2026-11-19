@echo off
taskkill /FI "WINDOWTITLE eq AI-Simulation API*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq AI-Simulation Agent*" /T /F >nul 2>&1
echo AI-Simulation stopped.
