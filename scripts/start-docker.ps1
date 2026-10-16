<#
.SYNOPSIS
    Inicializa o ecossistema completo CashFlow via Docker Compose (APIs, Postgres, RabbitMQ).
#>
param(
    [switch]$Build,
    [switch]$Tools,
    [switch]$NoBrowser
)

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "     Inicializando Ecossistema CashFlow no Docker      " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Verificar se o Docker Daemon está acessível
Write-Host "Verificando conectividade com o Docker..." -ForegroundColor Yellow
$dockerReady = $false
try {
    $null = docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        $dockerReady = $true
    }
} catch {
    $dockerReady = $false
}

if (-not $dockerReady) {
    Write-Host "`n[AVISO] O Docker Desktop precisa estar aberto para executar os contêineres." -ForegroundColor Red
    Write-Host "Abra o Docker Desktop pelo atalho do Windows e aguarde o ícone da baleia estabilizar." -ForegroundColor Yellow
    Write-Host "Em seguida, execute este script novamente:`n  powershell -File scripts/start-docker.ps1`n" -ForegroundColor White
    exit 1
}

Write-Host "[OK] Docker Desktop está em execução e pronto." -ForegroundColor Green

# 2. Executar Docker Compose
$cmd = "docker compose up -d"
if ($Build) {
    $cmd = "docker compose up -d --build"
}
if ($Tools) {
    $cmd += " --profile tools"
}

Write-Host "`nExecutando: $cmd" -ForegroundColor Cyan
Invoke-Expression $cmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERRO] Falha ao subir os contêineres." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nVerificando status dos contêineres..." -ForegroundColor Yellow
Start-Sleep -Seconds 3
docker compose ps

Write-Host "`n======================================================" -ForegroundColor Green
Write-Host "  Serviços disponíveis no ambiente Docker:" -ForegroundColor Green
Write-Host "  * Swagger Lançamentos:  http://localhost:5101/swagger" -ForegroundColor White
Write-Host "  * Swagger Consolidação: http://localhost:5102/swagger" -ForegroundColor White
Write-Host "  * RabbitMQ Management:  http://localhost:15672 (verity / verity_secret)" -ForegroundColor White
Write-Host "  * PostgreSQL:           localhost:5433 (cashflow_db)" -ForegroundColor White
Write-Host "======================================================" -ForegroundColor Green

if (-not $NoBrowser) {
    Write-Host "`nAbrindo Swagger das APIs no seu navegador padrão..." -ForegroundColor Cyan
    Start-Process "http://localhost:5101/swagger"
    Start-Process "http://localhost:5102/swagger"
}
