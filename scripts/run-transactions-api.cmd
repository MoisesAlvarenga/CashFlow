@echo off
title CashFlow - Transactions API (Port 5101)
cd /d "%~dp0.."
echo ======================================================
echo   CashFlow - Transactions API (Port 5101)
echo   Swagger: http://localhost:5101/swagger
echo ======================================================
dotnet run --project src\Transactions\CashFlow.Transactions.Api --no-build
pause
