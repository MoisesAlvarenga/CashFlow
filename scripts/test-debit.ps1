$ErrorActionPreference = 'Stop'

Write-Host "1. Gerando token JWT..." -ForegroundColor Cyan
$authBody = @{ clientId = "verity-client"; clientSecret = "verity-secret" } | ConvertTo-Json
$authResponse = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/auth/token" -Method Post -Body $authBody -ContentType "application/json"
$token = $authResponse.accessToken
Write-Host "   Token obtido com sucesso!" -ForegroundColor Green

Write-Host "`n2. Criando transacao de DEBITO..." -ForegroundColor Cyan
$idempotencyKey = [guid]::NewGuid().ToString()
$headers = @{
    Authorization = "Bearer $token"
    "Idempotency-Key" = $idempotencyKey
}
$today = (Get-Date).ToString("yyyy-MM-dd")
$txBody = @{
    amount = 150.25
    type = 2 # Debit
    date = $today
    description = "Pagamento Fornecedor (Debito)"
} | ConvertTo-Json

$txResponse = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/transactions" -Method Post -Headers $headers -Body $txBody -ContentType "application/json"
Write-Host "   Debito registrado! ID: $($txResponse.transactionId), Status: $($txResponse.status)" -ForegroundColor Green

Write-Host "`n3. Aguardando processamento da Outbox e Consumer RabbitMQ (6s)..." -ForegroundColor Cyan
Start-Sleep -Seconds 6

Write-Host "`n4. Consultando Saldo Diario Atualizado..." -ForegroundColor Cyan
$balance = Invoke-RestMethod -Uri "http://localhost:5102/api/v1/daily-balances/$today" -Method Get -Headers @{ Authorization = "Bearer $token" }
Write-Host "   Saldo Consolidado para ${today}:" -ForegroundColor Green
Write-Host "   - Total Credit: R$ $($balance.totalCredits)"
Write-Host "   - Total Debit:  R$ $($balance.totalDebits)"
Write-Host "   - Net Balance:  R$ $($balance.netBalance)"
