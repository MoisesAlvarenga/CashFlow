@echo off
title CashFlow - .NET Aspire AppHost & Dashboard
cd /d "%~dp0.."
echo ======================================================
echo   CashFlow - .NET Aspire AppHost & Dashboard
echo   Dashboard: https://localhost:17180 (ou http://localhost:15180)
echo ======================================================
dotnet run --project src\Aspire\CashFlow.AppHost --no-build
pause
