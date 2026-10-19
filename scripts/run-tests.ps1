<#
.SYNOPSIS
    Script automatizado para execução de testes unitários e testes de carga (K6) no CashFlow.
.DESCRIPTION
    Facilita a execução de testes locais e geração de relatórios de cobertura e qualidade.
    
    Exemplos de uso:
      .\scripts\run-tests.ps1                      # Executa a suíte de 51 testes unitários
      .\scripts\run-tests.ps1 -OpenReport          # Executa testes unitários e abre dashboard no navegador
      .\scripts\run-tests.ps1 -Smoke               # Executa o teste de carga rápido K6 (10s, 2 VUs)
      .\scripts\run-tests.ps1 -Load                # Executa o teste de carga completo K6 (30s, até 10 VUs)
      .\scripts\run-tests.ps1 -All -OpenReport     # Executa testes unitários, testes K6 e abre relatório
#>
param(
    [switch]$Unit,
    [switch]$Smoke,
    [switch]$Load,
    [switch]$All,
    [switch]$OpenReport
)

$ErrorActionPreference = "Stop"

if (-not ($Unit -or $Smoke -or $Load -or $All)) {
    $Unit = $true
}

if ($All) {
    $Unit = $true
    $Load = $true
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "       CashFlow - Suíte de Testes Automatizada        " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Execução de Testes Unitários
if ($Unit) {
    Write-Host "`n[1/2] Executando 51 Testes Unitários (.NET 9 + Cobertura XPlat)..." -ForegroundColor Yellow
    
    $projectPath = Join-Path $PSScriptRoot "..\tests\CashFlow.UnitTests\CashFlow.UnitTests.csproj"
    
    & dotnet test $projectPath --logger trx --collect:"XPlat Code Coverage"
    $unitExitCode = $LASTEXITCODE

    if ($unitExitCode -eq 0) {
        Write-Host "`n[SUCESSO] Todos os 51 testes unitários foram executados com êxito!" -ForegroundColor Green
    } else {
        Write-Host "`n[FALHA] Alguns testes unitários falharam (Código: $unitExitCode)." -ForegroundColor Red
        if (-not ($Smoke -or $Load)) {
            exit $unitExitCode
        }
    }
}

# 2. Execução de Testes de Carga com K6
if ($Smoke -or $Load) {
    Write-Host "`n[2/2] Verificando ambiente para Testes de Carga K6..." -ForegroundColor Yellow
    
    $k6Cmd = Get-Command k6 -ErrorAction SilentlyContinue
    if (-not $k6Cmd) {
        Write-Host "[ERRO] O executável do k6 não foi encontrado no PATH." -ForegroundColor Red
        Write-Host "Instale via 'winget install k6 --source winget' ou baixe em https://k6.io" -ForegroundColor Yellow
        exit 1
    }

    # Verificar se a API de Lançamentos está respondendo
    $apiOnline = $false
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5101/swagger/v1/swagger.json" -TimeoutSec 3 -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) { $apiOnline = $true }
    } catch {
        $apiOnline = $false
    }

    if (-not $apiOnline) {
        Write-Host "[AVISO] A API de Lançamentos (http://localhost:5101) não parece estar em execução." -ForegroundColor Yellow
        Write-Host "Para rodar os testes de carga, inicie a aplicação primeiro via:`n  .\scripts\start-docker.ps1`n" -ForegroundColor DarkYellow
    } else {
        if ($Smoke) {
            Write-Host "`nExecutando K6 Smoke Test (10s, 2 VUs)..." -ForegroundColor Cyan
            $smokeScript = Join-Path $PSScriptRoot "..\tests\load\k6-smoke-test.js"
            & k6 run $smokeScript
        }

        if ($Load) {
            Write-Host "`nExecutando K6 Load Test Completo (30s, até 10 VUs)..." -ForegroundColor Cyan
            $loadScript = Join-Path $PSScriptRoot "..\tests\load\k6-load-test.js"
            & k6 run $loadScript
        }
    }
}

# 3. Atualização Dinâmica do Dashboard do Solidify
Write-Host "`nAtualizando Dashboard do Solidify com as novas explicações e resultados..." -ForegroundColor Yellow
$generateReport = Join-Path $PSScriptRoot "generate-solidify-report.ps1"
if (Test-Path $generateReport) {
    & $generateReport
}

if ($OpenReport) {
    Write-Host "Abrindo Dashboard no seu navegador padrão..." -ForegroundColor Cyan
    $openBrowser = Join-Path $PSScriptRoot "open-browser.ps1"
    if (Test-Path $openBrowser) {
        if ($Smoke -or $Load) {
            & $openBrowser -K6
        } else {
            & $openBrowser -Tests
        }
    }
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host "        Execução de Testes Finalizada com Sucesso!     " -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Cyan
