<#
.SYNOPSIS
    Inicia os microsserviços CashFlow (Transactions API e Consolidation API) em terminais separados do Windows.
#>
param(
    [switch]$NoBrowser
)

$rootDir = Split-Path -Path $PSScriptRoot -Parent

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "   Iniciando APIs CashFlow em Terminais Separados     " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Garantir que a infraestrutura (PostgreSQL, RabbitMQ, Redis) está ativa no Docker
Write-Host "`n[1/3] Verificando e subindo serviços de infraestrutura (Postgres, RabbitMQ, Redis)..." -ForegroundColor Yellow
& docker compose up -d postgres rabbitmq redis
& docker stop cashflow-transactions-api cashflow-consolidation-api 2>$null

# 2. Iniciar Transactions API em janela separada de terminal
Write-Host "`n[2/3] Abrindo Transactions API (Porta 5101) em novo terminal..." -ForegroundColor Green
Start-Process cmd.exe -ArgumentList "/c `"$PSScriptRoot\run-transactions-api.cmd`""

# 3. Iniciar Consolidation API em janela separada de terminal
Write-Host "`n[3/3] Abrindo Consolidation API (Porta 5102) em novo terminal..." -ForegroundColor Green
Start-Process cmd.exe -ArgumentList "/c `"$PSScriptRoot\run-consolidation-api.cmd`""

Write-Host "`n======================================================" -ForegroundColor Green
Write-Host "  APIs em execução nos terminais dedicados:" -ForegroundColor Green
Write-Host "  * Swagger Lançamentos:  http://localhost:5101/swagger" -ForegroundColor White
Write-Host "  * Swagger Consolidação: http://localhost:5102/swagger" -ForegroundColor White
Write-Host "  * RabbitMQ Management:  http://localhost:15672 (verity / verity_secret)" -ForegroundColor White
Write-Host "  * PostgreSQL:           localhost:5433 (cashflow_db)" -ForegroundColor White
Write-Host "  * Redis Cache:          localhost:6379" -ForegroundColor White
Write-Host "======================================================" -ForegroundColor Green

if (-not $NoBrowser) {
    Start-Sleep -Seconds 5
    Write-Host "`nAbrindo Swagger das APIs no seu navegador padrão..." -ForegroundColor Cyan
    Start-Process "http://localhost:5101/swagger"
    Start-Process "http://localhost:5102/swagger"
}
