# Rebuilds the "Screenshots" block of README.md from the pictures in docs\screenshots.
# Usage (from the repository folder):   powershell -ExecutionPolicy Bypass -File tools\update-gallery.ps1
$root   = Split-Path -Parent $PSScriptRoot
$dir    = Join-Path $root 'docs\screenshots'
$readme = Join-Path $root 'README.md'

$files = Get-ChildItem -Path $dir -Filter *.png -ErrorAction SilentlyContinue | Sort-Object Name
if (-not $files) { Write-Host 'No .png files found in docs\screenshots - copy your screenshots there first.'; exit 1 }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('## Screenshots')
[void]$sb.AppendLine()
[void]$sb.AppendLine('<p align="center">')
foreach ($f in $files) {
    $alt = ($f.BaseName -replace '^\d+-', '') -replace '-', ' '
    [void]$sb.AppendLine("  <img src=""docs/screenshots/$($f.Name)"" width=""49%"" alt=""$alt"">")
}
[void]$sb.AppendLine('</p>')
$block = "<!-- screenshots:start -->`r`n" + $sb.ToString().TrimEnd() + "`r`n<!-- screenshots:end -->"

$text = Get-Content -Path $readme -Raw -Encoding UTF8
$pattern = '(?s)<!-- screenshots:start -->.*?<!-- screenshots:end -->'
if ($text -notmatch $pattern) { Write-Host 'README.md has no screenshots markers.'; exit 1 }
$text = [regex]::Replace($text, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $block })
Set-Content -Path $readme -Value $text -Encoding UTF8
Write-Host "Done: $($files.Count) screenshot(s) added to README.md"
