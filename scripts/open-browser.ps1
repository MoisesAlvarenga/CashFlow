<#
.SYNOPSIS
    Abre os relatórios do Archify, Solidify e SonarQube diretamente no seu navegador padrão.
.DESCRIPTION
    Utiliza o Windows ShellExecute para garantir que os arquivos HTML e dashboards
    sejam abertos no navegador (Chrome, Edge, etc.) em vez de abrirem no editor de texto da IDE.
#>
param(
    [switch]$Archify,
    [switch]$Solidify,
    [switch]$Sonar,
    [switch]$All = $true
)

$archifyHtml = "$PSScriptRoot\..\.archify\architecture-cash-flow-20261015-133255\cash-flow.html"
$solidifyHtml = "$PSScriptRoot\..\.solidify\out\release-report.html"
$sonarUrl = "http://localhost:9000/dashboard?id=desafio_tecnico_Verity"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "       Abrindo Dashboards no Navegador Padrão          " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

if ($Archify -or ($All -and -not ($Solidify -or $Sonar))) {
    if (Test-Path $archifyHtml) {
        Write-Host "Abrindo Diagrama de Arquitetura Archify no Navegador..." -ForegroundColor Green
        Start-Process (Resolve-Path $archifyHtml).Path
    } else {
        Write-Host "[AVISO] Arquivo do Archify não encontrado em $archifyHtml" -ForegroundColor DarkYellow
    }
}

if ($Solidify -or ($All -and -not ($Archify -or $Sonar))) {
    if (Test-Path $solidifyHtml) {
        Write-Host "Abrindo Relatório Solidify no Navegador..." -ForegroundColor Green
        Start-Process (Resolve-Path $solidifyHtml).Path
    } else {
        Write-Host "[AVISO] Arquivo do Solidify não encontrado em $solidifyHtml" -ForegroundColor DarkYellow
    }
}

if ($Sonar -or ($All -and -not ($Archify -or $Solidify))) {
    Write-Host "Abrindo Dashboard SonarQube no Navegador..." -ForegroundColor Green
    Start-Process $sonarUrl
}

Write-Host "`nDashboards abertos no seu navegador com sucesso!" -ForegroundColor Cyan
