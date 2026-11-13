# Fires exactly one alarm through the whole pipeline: used to produce a single end-to-end trace.
# The API is protected with OAuth scopes, so the script first obtains a client-credentials token.
$ErrorActionPreference = 'Stop'

$token = (Invoke-RestMethod -Uri 'http://localhost:5082/connect/token' -Method Post `
    -ContentType 'application/x-www-form-urlencoded' `
    -Body @{ grant_type = 'client_credentials'; client_id = 'alarmhub-console'; client_secret = 'dev-secret-change-me'; scope = 'alarms.read alarms.write' }).access_token

$auth = @{ Authorization = "Bearer $token" }

function Post-Json([string]$Uri, $Body, [hashtable]$Headers = @{}) {
    Invoke-RestMethod -Uri $Uri -Method Post -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 6) -Headers $Headers
}

$suffix = [guid]::NewGuid().ToString('N').Substring(0, 6)

$customer = Post-Json 'http://localhost:5080/api/customers' @{ name = "Trace Customer $suffix"; email = "trace+$suffix@alarmhub.local" } $auth
$site = Post-Json 'http://localhost:5080/api/sites' @{
    customerId = $customer.id; name = "Trace Site $suffix"; street = 'Calle Sol 1'; city = 'Malaga'
    postalCode = '29001'; countryCode = 'ES'; timeZoneId = 'Europe/Madrid'
} $auth
$panel = Post-Json 'http://localhost:5080/api/panels' @{ siteId = $site.id; serialNumber = "TRACE-$suffix"; model = 'SectorPro 4G' } $auth

$event = @{
    eventId        = [guid]::NewGuid()
    panelId        = $panel.id
    occurredAt     = (Get-Date).ToUniversalTime().ToString('o')
    type           = 'AlarmTriggered'
    sequenceNumber = 7
    payload        = @{ zone = '1' }
}

$result = Post-Json 'http://localhost:5081/device-events' $event @{ 'X-Panel-Key' = $panel.apiKey }

Write-Host ("event {0} -> {1}" -f $result.eventId, $result.status) -ForegroundColor Green
Write-Host ("site {0} / panel {1}" -f $site.id, $panel.id) -ForegroundColor DarkGray
