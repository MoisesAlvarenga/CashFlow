<#
.SYNOPSIS
    Abre os relatórios do Archify, Solidify, SonarQube e Swagger no navegador padrão.
.DESCRIPTION
    Utiliza o Windows ShellExecute para garantir que os dashboards e interfaces Swagger
    sejam abertos no navegador (Chrome, Edge, etc.) em vez de abrirem no editor de texto da IDE.
#>
param(
    [switch]$Archify,
    [switch]$Solidify,
    [switch]$Sonar,
    [switch]$Tests,
    [switch]$Swagger,
    [switch]$SwaggerTransactions,
    [switch]$SwaggerConsolidation,
    [switch]$All = $false
)

$archifyHtml = "$PSScriptRoot\..\.archify\architecture-cash-flow-20261015-133255\cash-flow.html"
$solidifyHtml = "$PSScriptRoot\..\.solidify\out\release-report.html"
$sonarUrl = "http://localhost:9000/dashboard?id=desafio_tecnico_Verity"
$swaggerTransactionsUrl = "http://localhost:5101/swagger"
$swaggerConsolidationUrl = "http://localhost:5102/swagger"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "       Abrindo Dashboards no Navegador Padrão          " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# Caso nenhum switch específico tenha sido passado, abre as ferramentas principais
$noSpecificSwitch = -not ($Archify -or $Solidify -or $Sonar -or $Tests -or $Swagger -or $SwaggerTransactions -or $SwaggerConsolidation)

if ($Tests) {
    if (Test-Path $solidifyHtml) {
        Write-Host "Abrindo Aba de Resultados dos Testes no Solidify..." -ForegroundColor Green
        $uri = ([System.Uri](Resolve-Path $solidifyHtml).Path).AbsoluteUri + "#tab-tests"
        Start-Process $uri
    } else {
        Write-Host "[AVISO] Arquivo do Solidify não encontrado em $solidifyHtml" -ForegroundColor DarkYellow
    }
    exit 0
}

if ($SwaggerTransactions -or $Swagger) {
    Write-Host "Abrindo Swagger da API de Lançamentos (Porta 5101)..." -ForegroundColor Green
    Start-Process $swaggerTransactionsUrl
}

if ($SwaggerConsolidation -or $Swagger) {
    Write-Host "Abrindo Swagger da API de Consolidação (Porta 5102)..." -ForegroundColor Green
    Start-Process $swaggerConsolidationUrl
}

if ($Archify -or $All -or $noSpecificSwitch) {
    if (Test-Path $archifyHtml) {
        Write-Host "Abrindo Diagrama de Arquitetura Archify no Navegador..." -ForegroundColor Green
        Start-Process (Resolve-Path $archifyHtml).Path
    } else {
        Write-Host "[AVISO] Arquivo do Archify não encontrado em $archifyHtml" -ForegroundColor DarkYellow
    }
}

if ($Solidify -or $All -or $noSpecificSwitch) {
    if (Test-Path $solidifyHtml) {
        Write-Host "Abrindo Relatório Solidify no Navegador..." -ForegroundColor Green
        Start-Process (Resolve-Path $solidifyHtml).Path
    } else {
        Write-Host "[AVISO] Arquivo do Solidify não encontrado em $solidifyHtml" -ForegroundColor DarkYellow
    }
}

if ($Sonar -or $All) {
    Write-Host "Abrindo Dashboard SonarQube no Navegador..." -ForegroundColor Green
    Start-Process $sonarUrl
}

Write-Host "`nDashboards abertos no seu navegador com sucesso!" -ForegroundColor Cyan
