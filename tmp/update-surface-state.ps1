$files = Get-ChildItem -Path 'plan/v0' -Recurse -Filter '*.md'
foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw
    if ($content -match 'Status: Done' -and $content -match 'Surface state: Planned') {
        $newContent = $content -replace 'Surface state: Planned', 'Surface state: Done'
        Set-Content $f.FullName -Value $newContent -NoNewline
        Write-Output "Updated: $($f.Name)"
    }
}