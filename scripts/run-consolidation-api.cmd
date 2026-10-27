@echo off
title CashFlow - Consolidation API (Port 5102)
cd /d "%~dp0.."
echo ======================================================
echo   CashFlow - Consolidation API (Port 5102)
echo   Swagger: http://localhost:5102/swagger
echo ======================================================
dotnet run --project src\Consolidation\CashFlow.Consolidation.Api --no-build
pause
