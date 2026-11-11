$env:Path = 'C:\Program Files\Docker\Docker\resources\bin;' + $env:Path

# NOTE: identifiers are quoted with double quotes in SQL, and PowerShell strips them when passing
# a native argument, so the verification uses SELECT * (all columns) instead of a column list.
function Sql([string]$db, [string]$query) {
    docker exec alarmhub-postgres psql -U alarm -d $db -t -A -F ' | ' -c $query
}

Write-Host '=== Incident opened by processing ===' -ForegroundColor Cyan
Sql 'alarmhub_processing' 'select * from incidents order by 1 limit 3;'

Write-Host '=== Notification delivered ===' -ForegroundColor Cyan
Sql 'alarmhub_notifications' 'select * from notifications order by 1 limit 3;'

Write-Host '=== Counts ===' -ForegroundColor Cyan
"incidents:                        " + (Sql 'alarmhub_processing' 'select count(*) from incidents;')
"notifications:                    " + (Sql 'alarmhub_notifications' 'select count(*) from notifications;')
"ingest outbox published:          " + (Sql 'alarmhub_ingest' 'select count(*) from outbox_messages;')
"processing outbox published:      " + (Sql 'alarmhub_processing' 'select count(*) from outbox_messages;')
