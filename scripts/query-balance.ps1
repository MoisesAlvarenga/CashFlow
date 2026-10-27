param(
    [string]$Date = (Get-Date).ToString("yyyy-MM-dd")
)

$authBody = @{ clientId = "verity-client"; clientSecret = "verity-secret" } | ConvertTo-Json
$authResponse = Invoke-RestMethod -Uri "http://localhost:5101/api/v1/auth/token" -Method Post -Body $authBody -ContentType "application/json"
$token = $authResponse.accessToken

Write-Host "Consultando saldo diário consolidado para a data: $Date..." -ForegroundColor Cyan
try {
    $res = Invoke-RestMethod -Uri "http://localhost:5102/api/v1/daily-balances/$Date" -Method Get -Headers @{ Authorization = "Bearer $token" }
    $res | ConvertTo-Json
} catch {
    Write-Host "Aviso: $($_.Exception.Message)" -ForegroundColor Yellow
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host "Detalhe: $($reader.ReadToEnd())" -ForegroundColor DarkGray
    }
}
