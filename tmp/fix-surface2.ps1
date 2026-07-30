$doneFiles = @(
    'plan/v0/yemeksepeti/V0-YSP-001-partner-api-contract.md',
    'plan/v0/qr-relay/V0-QRG-001-relay-threat-and-feasibility.md',
    'plan/v0/printing/V0-PRN-001-printer-contract.md'
)
$inProgressFiles = @(
    'plan/v0/hugin-t300/V0-HUG-001-integration-contract.md',
    'plan/v0/qnb-esolutions/V0-QNB-001-integration-contract.md',
    'plan/v0/meal-card/V0-MCD-001-provider-contract.md'
)

foreach ($f in $doneFiles) {
    $c = Get-Content $f -Raw
    $c = $c -replace 'Surface state: Planned', 'Surface state: Done'
    Set-Content $f -Value $c -NoNewline
    Write-Output "Done: $f"
}

foreach ($f in $inProgressFiles) {
    $c = Get-Content $f -Raw
    $c = $c -replace 'Surface state: Planned', 'Surface state: InProgress'
    Set-Content $f -Value $c -NoNewline
    Write-Output "InProgress: $f"
}