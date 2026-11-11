# Fires one alarm and reports how the OTel collector saw it: one trace, several services.
$ErrorActionPreference = 'Stop'
$env:Path = 'C:\Program Files\Docker\Docker\resources\bin;' + $env:Path

& "$PSScriptRoot\fire-alarm.ps1"

Start-Sleep -Seconds 6

# docker writes container logs to stderr; PS 5.1 turns that into error records, so relax EAP here.
$previous = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$raw = docker logs alarmhub-otel --since 2m 2>&1
$ErrorActionPreference = $previous

$lines = @($raw | ForEach-Object { $_.ToString() })

$currentService = $null
$spans = @()

foreach ($line in $lines) {
    if ($line -match 'service\.name:\s+Str\(([^)]+)\)') {
        $currentService = $Matches[1]
    }
    if ($line -match 'Trace ID\s*:\s*([0-9a-f]{32})') {
        $spans += [pscustomobject]@{ TraceId = $Matches[1]; Service = $currentService }
    }
}

if ($spans.Count -eq 0) {
    Write-Host 'No spans in the collector yet. Is the collector running and OTLP endpoint set?' -ForegroundColor Red
    return
}

Write-Host ''
Write-Host '=== Traces that span more than one service ===' -ForegroundColor Cyan

$report = $spans |
    Group-Object TraceId |
    ForEach-Object {
        [pscustomobject]@{
            TraceId  = $_.Name
            Spans    = $_.Count
            Services = (($_.Group.Service | Sort-Object -Unique) -join ', ')
        }
    } |
    Where-Object { ($_.Services -split ', ').Count -gt 1 } |
    Sort-Object Spans -Descending |
    Select-Object -First 5

if ($report) {
    $report | Format-Table -AutoSize | Out-String | Write-Host
}
else {
    Write-Host 'Spans arrived but no trace spans two services yet; check traceparent propagation.' -ForegroundColor Yellow
    $spans | Group-Object TraceId | ForEach-Object {
        "{0}  spans={1}  services={2}" -f $_.Name, $_.Count, (($_.Group.Service | Sort-Object -Unique) -join ', ')
    } | Select-Object -First 5
}
