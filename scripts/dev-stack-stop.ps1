# Stops the local dev stack processes listening on the app ports.
$ports = 3000, 5080, 8000
foreach ($port in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    $pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($procId in $pids) {
        if ($procId -and $procId -ne 0) {
            Write-Host "Stopping pid $procId listening on :$port"
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        }
    }
}
Write-Host "Done. PostgreSQL and Mailpit (if running) were left alone."
