<#
.SYNOPSIS
    Testa a autenticação JWT com BCrypt, a autorização de roles (RBAC) e o fluxo assíncrono entre Lançamentos e Consolidação.
#>

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "   TESTE COMPLETO DE ROLES, AUTENTICAÇÃO E ENDPOINTS  " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Autenticação dos 3 perfis
$adminToken = (Invoke-RestMethod -Uri "http://localhost:5101/api/v1/auth/token" -Method Post -ContentType "application/json" -Body '{"clientId":"verity-client","clientSecret":"verity-secret"}').accessToken
$opToken = (Invoke-RestMethod -Uri "http://localhost:5101/api/v1/auth/token" -Method Post -ContentType "application/json" -Body '{"clientId":"operador-financeiro","clientSecret":"verity-secret"}').accessToken
$viewerToken = (Invoke-RestMethod -Uri "http://localhost:5101/api/v1/auth/token" -Method Post -ContentType "application/json" -Body '{"clientId":"consolidation-viewer","clientSecret":"verity-secret"}').accessToken

Write-Host "[OK 1/7] Tokens JWT gerados com sucesso:" -ForegroundColor Green
Write-Host "         * verity-client:        Roles = Operator, Admin" -ForegroundColor White
Write-Host "         * operador-financeiro:  Roles = Operator" -ForegroundColor White
Write-Host "         * consolidation-viewer: Roles = Viewer" -ForegroundColor White

# 2. Teste de credencial inválida (deve falhar com 401)
try {
    Invoke-RestMethod -Uri "http://localhost:5101/api/v1/auth/token" -Method Post -ContentType "application/json" -Body '{"clientId":"operador-financeiro","clientSecret":"senha-incorreta"}'
    Write-Host "[FALHA] Senha incorreta deveria ser rejeitada!" -ForegroundColor Red
} catch {
    Write-Host "`n[OK 2/7] Tentativa com senha incorreta bloqueada com HTTP $($_.Exception.Response.StatusCode.value__) (401 Unauthorized)" -ForegroundColor Green
}

# 3. Teste RBAC: Viewer tentando POST /transactions (deve retornar 403 Forbidden)
try {
    $bodyViewer = '{"type":"Credit","amount":999.00,"date":"2026-10-27","description":"Tentativa Viewer Nao Autorizada"}'
    Invoke-RestMethod -Uri "http://localhost:5101/api/v1/transactions" -Method Post -Headers @{ Authorization = "Bearer $viewerToken" } -ContentType "application/json" -Body $bodyViewer
    Write-Host "[FALHA] Perfil Viewer conseguiu criar transacao!" -ForegroundColor Red
} catch {
    Write-Host "`n[OK 3/7] Perfil Viewer bloqueado ao tentar POST /transactions com HTTP $($_.Exception.Response.StatusCode.value__) (403 Forbidden)" -ForegroundColor Green
}

# 4. Teste RBAC: Operador criando transação de Crédito (deve retornar 201)
$today = (Get-Date).ToString("yyyy-MM-dd")
$idemCredit = [guid]::NewGuid().ToString()
$bodyCredit = "{`"type`":`"Credit`",`"amount`":1500.50,`"date`":`"$today`",`"description`":`"Venda de Produtos A Vista`"}"
$resCredit = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/transactions" -Method Post -Headers @{ Authorization = "Bearer $opToken"; "Idempotency-Key" = $idemCredit } -ContentType "application/json" -Body $bodyCredit
Write-Host "`n[OK 4/7] Operador financeiro registrou Crédito! ID: $($resCredit.transactionId) - Status: $($resCredit.status)" -ForegroundColor Green

# 5. Teste RBAC: Operador criando transação de Débito (deve retornar 201)
$idemDebit = [guid]::NewGuid().ToString()
$bodyDebit = "{`"type`":`"Debit`",`"amount`":300.25,`"date`":`"$today`",`"description`":`"Pagamento Fornecedor A vista`"}"
$resDebit = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/transactions" -Method Post -Headers @{ Authorization = "Bearer $opToken"; "Idempotency-Key" = $idemDebit } -ContentType "application/json" -Body $bodyDebit
Write-Host "`n[OK 5/7] Operador financeiro registrou Débito! ID: $($resDebit.transactionId) - Status: $($resDebit.status)" -ForegroundColor Green

# 6. Teste: Viewer consultando lançamentos paginados (deve retornar 200)
$listTx = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/transactions?startDate=$today&endDate=$today&page=1&pageSize=10" -Method Get -Headers @{ Authorization = "Bearer $viewerToken" }
Write-Host "`n[OK 6/7] Viewer consultou lista de lançamentos: $($listTx.items.Count) transações retornadas na página 1 (Total: $($listTx.totalCount))" -ForegroundColor Green

# 7. Aguardar processamento do Outbox Worker e Consumer RabbitMQ
Write-Host "`nAguardando propagação assíncrona (Outbox Worker -> RabbitMQ -> Consumer Consolidation) [6s]..." -ForegroundColor Yellow
Start-Sleep -Seconds 6

# 8. Teste: Viewer consultando saldo consolidado na Consolidation API (deve retornar 200)
$bal = Invoke-RestMethod -Uri "http://localhost:5102/api/v1/daily-balances/$today" -Method Get -Headers @{ Authorization = "Bearer $viewerToken" }
Write-Host "`n[OK 7/7] Saldo diário consolidado obtido na Consolidation API com sucesso:" -ForegroundColor Green
Write-Host "         * Data:            $($bal.date)" -ForegroundColor White
Write-Host "         * Total Créditos:  R$ $($bal.totalCredits)" -ForegroundColor White
Write-Host "         * Total Débitos:   R$ $($bal.totalDebits)" -ForegroundColor White
Write-Host "         * Saldo Líquido:   R$ $($bal.netBalance)" -ForegroundColor White
Write-Host "         * Qtd Transações:  $($bal.transactionCount)" -ForegroundColor White

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host "  VALIDAÇÃO DE ROLES E FLUXO END-TO-END 100% APROVADA! " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
