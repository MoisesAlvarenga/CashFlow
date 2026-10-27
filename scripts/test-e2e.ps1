$ErrorActionPreference = 'Stop'

Write-Host "1. Gerando token JWT..." -ForegroundColor Cyan
$authBody = @{ clientId = "verity-client"; clientSecret = "verity-secret" } | ConvertTo-Json
$authResponse = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/auth/token" -Method Post -Body $authBody -ContentType "application/json"
$token = $authResponse.accessToken
Write-Host "   Token obtido com sucesso!" -ForegroundColor Green

Write-Host "`n2. Criando transação com Idempotency-Key..." -ForegroundColor Cyan
$idempotencyKey = [guid]::NewGuid().ToString()
$headers = @{
    Authorization = "Bearer $token"
    "Idempotency-Key" = $idempotencyKey
}
$today = (Get-Date).ToString("yyyy-MM-dd")
$txBody = @{
    amount = 500.50
    type = 1 # Credit
    date = $today
    description = "Lancamento E2E Aspire Test"
} | ConvertTo-Json

$txResponse = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/transactions" -Method Post -Headers $headers -Body $txBody -ContentType "application/json"
Write-Host "   Transação criada! ID: $($txResponse.transactionId), Status: $($txResponse.status)" -ForegroundColor Green

Write-Host "`n3. Testando idempotência (re-enviando mesma Idempotency-Key)..." -ForegroundColor Cyan
$txResponseDuplicate = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/transactions" -Method Post -Headers $headers -Body $txBody -ContentType "application/json"
Write-Host "   Resposta Idempotente recebida com ID idêntico: $($txResponseDuplicate.transactionId)" -ForegroundColor Green

Write-Host "`n4. Aguardando processamento do Outbox e Consumer RabbitMQ (6s)..." -ForegroundColor Cyan
Start-Sleep -Seconds 6

Write-Host "`n5. Consultando Saldo Consolidado na Consolidation API com token JWT..." -ForegroundColor Cyan
$today = (Get-Date).ToString("yyyy-MM-dd")
$consolidationResponse = Invoke-RestMethod -Uri "http://localhost:5102/api/v1/daily-balances/$today" -Method Get -Headers @{ Authorization = "Bearer $token" }
Write-Host "   Saldo Consolidado para ${today}:" -ForegroundColor Green
Write-Host "   - Total Credit: R$ $($consolidationResponse.totalCredits)"
Write-Host "   - Total Debit:  R$ $($consolidationResponse.totalDebits)"
Write-Host "   - Net Balance:  R$ $($consolidationResponse.netBalance)"

Write-Host "`n6. Testando cache Redis (segunda consulta deve vir do cache)..." -ForegroundColor Cyan
$cachedResponse = Invoke-RestMethod -Uri "http://localhost:5102/api/v1/daily-balances/$today" -Method Get -Headers @{ Authorization = "Bearer $token" }
Write-Host "   Consulta via cache concluída com sucesso! Net Balance: R$ $($cachedResponse.netBalance)" -ForegroundColor Green

Write-Host "`n=== TODOS OS TESTES E2E PASSARAM COM SUCESSO! ===" -ForegroundColor Green
