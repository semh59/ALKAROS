$files = @(
    'plan/v0/yemeksepeti/V0-YSP-001-partner-api-contract.md',
    'plan/v0/qr-relay/V0-QRG-001-relay-threat-and-feasibility.md',
    'plan/v0/printing/V0-PRN-001-printer-contract.md'
)
foreach ($f in $files) {
    $c = Get-Content $f -Raw
    if ($c -match 'Status: Planned') {
        $c = $c -replace 'Status: Planned', 'Status: Done'
        $c = $c -replace 'Surface state: Planned', 'Surface state: Done'
        $c = $c -replace 'Assignee: Unassigned \(exactly one person\)', 'Assignee: codex-v0-batch'
        Set-Content $f -Value $c -NoNewline
        Write-Output "Done: $f"
    }
}