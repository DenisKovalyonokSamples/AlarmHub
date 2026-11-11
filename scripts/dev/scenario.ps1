$ErrorActionPreference = 'Stop'

# Every run creates a fresh customer and panel so the scenario can be replayed without conflicts.
$suffix = [guid]::NewGuid().ToString('N').Substring(0, 6)

function Post-Json([string]$Uri, $Body, [hashtable]$Headers = @{}) {
    Invoke-RestMethod -Uri $Uri -Method Post -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 6) -Headers $Headers
}

Write-Host '=== 1. Create customer ===' -ForegroundColor Cyan
$customer = Post-Json 'http://localhost:5080/api/customers' @{ name = 'Nordic Homes AB'; email = ('ops+' + $suffix + '@nordichomes.example') }
$customer | ConvertTo-Json -Depth 3 | Write-Host

Write-Host '=== 2. Create site ===' -ForegroundColor Cyan
$site = Post-Json 'http://localhost:5080/api/sites' @{
    customerId  = $customer.id
    name        = 'Home - Fuengirola'
    street      = 'Calle Marbella 12'
    city        = 'Fuengirola'
    postalCode  = '29640'
    countryCode = 'ES'
    timeZoneId  = 'Europe/Madrid'
}
$site | ConvertTo-Json -Depth 3 | Write-Host

Write-Host '=== 3. Register panel (API key returned once) ===' -ForegroundColor Cyan
$panel = Post-Json 'http://localhost:5080/api/panels' @{ siteId = $site.id; serialNumber = ('SA-PANEL-' + $suffix); model = 'SectorPro 4G' }
$panel | ConvertTo-Json -Depth 3 | Write-Host

Write-Host '=== 4. Add sensor zone 3 ===' -ForegroundColor Cyan
$sensor = Post-Json "http://localhost:5080/api/panels/$($panel.id)/sensors" @{ zoneNumber = 3; type = 'Motion'; name = 'Hallway PIR' }
$sensor | ConvertTo-Json -Depth 3 | Write-Host

Write-Host '=== 5. Panel reports an alarm (X-Panel-Key) ===' -ForegroundColor Cyan
$eventId = [guid]::NewGuid()
$deviceEvent = @{
    eventId        = $eventId
    panelId        = $panel.id
    occurredAt     = (Get-Date).ToUniversalTime().ToString('o')
    type           = 'AlarmTriggered'
    sequenceNumber = 42
    payload        = @{ zone = '3'; signal = 'strong' }
}
$ingestResult = Post-Json 'http://localhost:5081/device-events' $deviceEvent @{ 'X-Panel-Key' = $panel.apiKey }
$ingestResult | ConvertTo-Json -Depth 3 | Write-Host

Write-Host '=== 6. Panel retries the same event (idempotency) ===' -ForegroundColor Cyan
$retry = Post-Json 'http://localhost:5081/device-events' $deviceEvent @{ 'X-Panel-Key' = $panel.apiKey }
$retry | ConvertTo-Json -Depth 3 | Write-Host

Write-Host '=== 7. Wrong key is rejected ===' -ForegroundColor Cyan
try {
    Post-Json 'http://localhost:5081/device-events' $deviceEvent @{ 'X-Panel-Key' = 'not-the-key' } | Out-Null
    Write-Host 'UNEXPECTED: request succeeded'
}
catch {
    Write-Host ("rejected with HTTP {0}" -f [int]$_.Exception.Response.StatusCode)
}

Write-Host '=== 8. Site aggregate with panel and sensor ===' -ForegroundColor Cyan
Invoke-RestMethod -Uri "http://localhost:5080/api/sites/$($site.id)" | ConvertTo-Json -Depth 5 | Write-Host

