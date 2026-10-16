$ErrorActionPreference = "Stop"
$token = (Get-Content .sonar_token).Trim()
$auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${token}:"))

$measures = Invoke-RestMethod -Uri "http://localhost:9000/api/measures/component?component=desafio_tecnico_Verity&metricKeys=bugs,vulnerabilities,code_smells,coverage,alert_status,duplicated_lines_density,security_hotspots" -Headers @{ Authorization = "Basic $auth" }
Write-Host "=== Medidas Globais do Projeto ===" -ForegroundColor Cyan
foreach ($m in $measures.component.measures) {
    Write-Host "$($m.metric): $($m.value)"
}

$issues = Invoke-RestMethod -Uri "http://localhost:9000/api/issues/search?componentKeys=desafio_tecnico_Verity&resolved=false&ps=100" -Headers @{ Authorization = "Basic $auth" }
Write-Host "`n=== Total de Issues Abertas: $($issues.total) ===" -ForegroundColor Cyan
$grouped = $issues.issues | Group-Object -Property type
foreach ($g in $grouped) {
    Write-Host "Tipo: $($g.Name) - Quantidade: $($g.Count)" -ForegroundColor Yellow
}

Write-Host "`n=== Principais Arquivos Afetados ===" -ForegroundColor Cyan
$fileGroups = $issues.issues | Group-Object -Property component
foreach ($f in $fileGroups | Select-Object -First 10) {
    Write-Host "$($f.Name) : $($f.Count) issues"
}

Write-Host "`n=== Amostra de Issues no Código ===" -ForegroundColor Cyan
foreach ($i in $issues.issues | Select-Object -First 10) {
    Write-Host "[$($i.severity)][$($i.type)] $($i.component) (Linha $($i.line)): $($i.message)"
}
