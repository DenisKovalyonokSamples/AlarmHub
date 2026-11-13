# Requests a client-credentials token from the local identity service.
# Usage: pwsh scripts/dev/get-token.ps1
$ErrorActionPreference = 'Stop'

$body = @{
    grant_type    = 'client_credentials'
    client_id     = 'alarmhub-console'
    client_secret = $env:ALARMHUB_CLIENT_SECRET ?? 'dev-secret-change-me'
    scope         = 'alarms.read alarms.write'
}

$token = Invoke-RestMethod -Uri 'http://localhost:5082/connect/token' -Method Post `
    -ContentType 'application/x-www-form-urlencoded' -Body $body

Write-Host ("token_type={0} expires_in={1}" -f $token.token_type, $token.expires_in) -ForegroundColor Green
Write-Host ("access_token: {0}..." -f $token.access_token.Substring(0, 40)) -ForegroundColor DarkGray

# The access token is returned for manual use in curl/Swagger.
$token.access_token
