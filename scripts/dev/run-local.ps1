# Starts the AlarmHub services locally against the docker compose stack.
# Usage: pwsh scripts/dev/run-local.ps1
$ErrorActionPreference = 'Stop'

Set-Location (Resolve-Path "$PSScriptRoot\..\..")

$env:ASPNETCORE_ENVIRONMENT = 'Development'

# Emulator connection string: the SDK needs UseDevelopmentEmulator=true to skip TLS.
$env:ConnectionStrings__ServiceBus = 'Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'

$logs = Join-Path $PSScriptRoot '..\..\.logs'
New-Item -ItemType Directory -Force $logs | Out-Null

Write-Host 'Starting API on http://localhost:5080 ...' -ForegroundColor Cyan
Start-Process dotnet -ArgumentList 'run','--project','src/AlarmHub.Api','--urls','http://localhost:5080','--no-build' `
    -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api.err') -WindowStyle Hidden

Write-Host 'Starting Ingest on http://localhost:5081 ...' -ForegroundColor Cyan
Start-Process dotnet -ArgumentList 'run','--project','src/AlarmHub.Ingest','--urls','http://localhost:5081','--no-build' `
    -RedirectStandardOutput (Join-Path $logs 'ingest.log') -RedirectStandardError (Join-Path $logs 'ingest.err') -WindowStyle Hidden

Write-Host 'Starting Processing ...' -ForegroundColor Cyan
Start-Process dotnet -ArgumentList 'run','--project','src/AlarmHub.Processing','--no-build' `
    -RedirectStandardOutput (Join-Path $logs 'processing.log') -RedirectStandardError (Join-Path $logs 'processing.err') -WindowStyle Hidden

Write-Host 'Starting Notifications ...' -ForegroundColor Cyan
Start-Process dotnet -ArgumentList 'run','--project','src/AlarmHub.Notifications','--no-build' `
    -RedirectStandardOutput (Join-Path $logs 'notifications.log') -RedirectStandardError (Join-Path $logs 'notifications.err') -WindowStyle Hidden

Start-Sleep -Seconds 12

foreach ($health in @('http://localhost:5080/health/live', 'http://localhost:5081/health/live')) {
    try {
        $status = (Invoke-WebRequest -Uri $health -UseBasicParsing -TimeoutSec 5).StatusCode
        Write-Host "$health -> $status" -ForegroundColor Green
    }
    catch {
        Write-Host "$health -> $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host 'Logs: .logs\*.log' -ForegroundColor DarkGray
