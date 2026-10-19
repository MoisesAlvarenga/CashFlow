<#
.SYNOPSIS
    Executa a análise do SonarQube integrada com o .NET e o Solidify.
.DESCRIPTION
    Inicia a sessão com dotnet-sonarscanner, compila o projeto, executa a suíte de testes
    com coleta de cobertura de código, finaliza a análise no SonarQube e prepara o ambiente
    para o Solidify avaliar o Quality Gate e os princípios SOLID.
#>
param(
    [string]$SonarUrl = "http://localhost:9000",
    [string]$ProjectKey = "desafio_tecnico_Verity",
    [string]$SonarToken = $env:SONAR_TOKEN,
    [switch]$StartDocker
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "   SonarQube + Solidify Analysis - Desafio Verity      " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Verificar se Docker deve ser iniciado
if ($StartDocker) {
    Write-Host "[1/5] Subindo contêiner do SonarQube via Docker Compose..." -ForegroundColor Yellow
    docker compose -f docker-compose.sonar.yml up -d
    Write-Host "Aguardando inicialização do SonarQube..." -ForegroundColor Yellow
    Start-Sleep -Seconds 15
}

# 2. Verificar acessibilidade do SonarQube
Write-Host "[2/5] Testando conexão com SonarQube em $SonarUrl..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$SonarUrl/api/system/status" -TimeoutSec 10
    Write-Host "SonarQube Online! Status: $($response.status)" -ForegroundColor Green
} catch {
    Write-Host "[AVISO] Não foi possível conectar a $SonarUrl imediatamente." -ForegroundColor DarkYellow
    Write-Host "Certifique-se de que o SonarQube está rodando (docker compose -f docker-compose.sonar.yml up -d)." -ForegroundColor DarkYellow
}

# 3. dotnet-sonarscanner begin
Write-Host "[3/5] Iniciando dotnet-sonarscanner begin..." -ForegroundColor Yellow
$beginArgs = @(
    "begin",
    "/k:$ProjectKey",
    "/d:sonar.host.url=$SonarUrl",
    "/d:sonar.exclusions=**/.archify/**,**/.agents/**,**/.solidify/**,**/.specs/**,**/bin/**,**/obj/**,**/out/**,**/tests/**,**/.claude/**,**/.cursor/**,**/.windsurf/**,**/appsettings*.json",
    "/d:sonar.coverage.exclusions=**/tests/**,**/Program.cs,**/Migrations/**",
    "/d:sonar.cs.opencover.reportsPaths=**/TestResults/**/coverage.opencover.xml,**/TestResults/**/coverage.cobertura.xml",
    "/d:sonar.cs.vstest.reportsPaths=**/TestResults/**/*.trx"
)

if (Test-Path ".sonar_token") {
    $fileToken = (Get-Content ".sonar_token").Trim()
    if (-not [string]::IsNullOrWhiteSpace($fileToken)) {
        $SonarToken = $fileToken
    }
}

if (-not [string]::IsNullOrWhiteSpace($SonarToken)) {
    $beginArgs += "/d:sonar.token=$SonarToken"
}

try {
    dotnet-sonarscanner @beginArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERRO] dotnet-sonarscanner begin falhou (exit code $LASTEXITCODE). Interrompendo." -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "[ERRO] Falha ao iniciar dotnet-sonarscanner: $_" -ForegroundColor Red
    exit 1
}

# 4. Build e Testes com Cobertura
Write-Host "[4/5] Compilando e executando testes com cobertura..." -ForegroundColor Yellow
if (Test-Path "*.sln") {
    dotnet build CashFlow.sln --no-incremental
    dotnet test tests/CashFlow.UnitTests/CashFlow.UnitTests.csproj --logger trx --collect:"XPlat Code Coverage"
} else {
    Write-Host "[AVISO] Nenhuma solução .sln encontrada na raiz ainda. Pulando build direto." -ForegroundColor DarkYellow
}

# 5. dotnet-sonarscanner end
Write-Host "[5/5] Finalizando análise no SonarQube (dotnet-sonarscanner end)..." -ForegroundColor Yellow
$endArgs = @("end")
if (-not [string]::IsNullOrWhiteSpace($SonarToken)) {
    $endArgs += "/d:sonar.token=$SonarToken"
}

try {
    dotnet-sonarscanner @endArgs
    Write-Host "`nAnálise SonarQube concluída com sucesso!" -ForegroundColor Green
    Write-Host "Métricas disponíveis no dashboard: $SonarUrl/dashboard?id=$ProjectKey" -ForegroundColor Cyan
} catch {
    Write-Host "[AVISO] dotnet-sonarscanner end encerrou com aviso/erro. Verifique os logs acima." -ForegroundColor DarkYellow
}

Write-Host "`nPróximo passo: execute o Solidify para validar o Quality Gate arquitetural (SOLID):" -ForegroundColor Cyan
Write-Host "  powershell -File scripts/solidify-audit.ps1 -Profile quick" -ForegroundColor White
