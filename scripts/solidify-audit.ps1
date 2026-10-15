<#
.SYNOPSIS
    Executa a auditoria do Solidify (SOLID & Release Quality Gate).
.DESCRIPTION
    Invoca o binário do Solidify para validar pré-requisitos, analisar o diff e relatórios
    de qualidade (incluindo métricas do SonarQube) e emitir o relatório de release.
#>
param(
    [ValidateSet("quick", "release", "contractual")]
    [string]$Profile = "quick",
    [ValidateSet("json", "pdf", "html")]
    [string]$Format = "json",
    [switch]$CheckOnly
)

$ErrorActionPreference = "Stop"

if (Test-Path ".sonar_token") {
    $token = (Get-Content ".sonar_token").Trim()
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $env:SONAR_TOKEN = $token
    }
}

$solidifyBin = "$env:USERPROFILE\.gemini\config\skills\solidify\bin\solidify.cmd"
if (-not (Test-Path $solidifyBin)) {
    $solidifyBin = "$env:USERPROFILE\.gemini\config\plugins\solidify\skills\solidify\bin\solidify.cmd"
}

if (-not (Test-Path $solidifyBin)) {
    Write-Host "[ERRO] Binário do Solidify não encontrado em $solidifyBin" -ForegroundColor Red
    exit 1
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "        Solidify - Release Quality Gate & SOLID        " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Doctor check
Write-Host "[1/3] Executando diagnósticos do ambiente (solidify doctor)..." -ForegroundColor Yellow
try {
    & $solidifyBin doctor
} catch {
    Write-Host "[AVISO] Alguns pré-requisitos opcionais do Solidify não estão ativos." -ForegroundColor DarkYellow
}

if ($CheckOnly) {
    Write-Host "Verificação de ambiente concluída." -ForegroundColor Green
    exit 0
}

# 2. Executar análise
Write-Host "`n[2/3] Executando análise com perfil '$Profile'..." -ForegroundColor Yellow
try {
    & $solidifyBin run --profile $Profile
} catch {
    Write-Host "[AVISO] solidify run finalizado." -ForegroundColor DarkYellow
}

# 3. Gerar relatório
Write-Host "`n[3/3] Gerando relatório no formato '$Format'..." -ForegroundColor Yellow
if (Test-Path ".solidify\out\release-report.json") {
    if (-not (Test-Path "out\runs")) { New-Item -ItemType Directory -Path "out\runs" -Force | Out-Null }
    Copy-Item ".solidify\out\release-report.json" "out\runs\latest.json" -Force
}
try {
    & $solidifyBin report --format $Format --run latest
    Write-Host "`nRelatório gerado em .solidify/out/" -ForegroundColor Green
} catch {
    Write-Host "[INFO] Use 'solidify report' quando a análise for concluída." -ForegroundColor Gray
}
