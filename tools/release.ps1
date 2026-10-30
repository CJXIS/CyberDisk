# Builds CyberDisk and prepares a GitHub release in one go.
#
#   powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Version 1.4.2
#
# 1) builds Release and puts CyberDisk.exe (+ its SHA-256) in the "release" folder,
# 2) commits and pushes your changes, creates and pushes the tag v<Version>,
# 3) creates the GitHub release with the .exe attached when the GitHub CLI ("gh") is installed;
#    otherwise it opens the "new release" page with the tag filled in - just drag release\CyberDisk.exe onto it and press Publish.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Repo = 'CJXIS/CyberDisk'
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$tag = "v$Version"

Write-Host "== Building $tag ==" -ForegroundColor Cyan
dotnet build CyberDisk.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'The build failed - fix the errors above and run the script again.' }

$exe = Get-ChildItem -Path bin\Release -Recurse -Filter CyberDisk.exe | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { throw 'CyberDisk.exe was not found under bin\Release.' }
New-Item -ItemType Directory -Force -Path release | Out-Null
Copy-Item $exe.FullName release\CyberDisk.exe -Force
$hash = (Get-FileHash release\CyberDisk.exe -Algorithm SHA256).Hash.ToLower()
"$hash  CyberDisk.exe" | Out-File -Encoding ascii release\CyberDisk.exe.sha256

# release notes = this version's section of CHANGELOG.md
$text = Get-Content CHANGELOG.md -Raw -Encoding UTF8
$m = [regex]::Match($text, "(?ms)^## \[$([regex]::Escape($Version))\].*?(?=^## \[|\z)")
$notes = if ($m.Success) { $m.Value.Trim() } else { "CyberDisk $Version" }
$notes += "`n`n---`n**SHA-256:** ``$hash```n`nWindows may show a SmartScreen notice for a new unsigned program: click **More info**, then **Run anyway**."
Set-Content -Path release\notes.md -Value $notes -Encoding UTF8

Write-Host "== Pushing to GitHub ==" -ForegroundColor Cyan
git add -A
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) { git commit -m "CyberDisk $Version" }
git push
git tag -f $tag
git push origin $tag --force

if (Get-Command gh -ErrorAction SilentlyContinue) {
    Write-Host "== Creating the release with gh ==" -ForegroundColor Cyan
    gh release view $tag --repo $Repo *> $null
    if ($LASTEXITCODE -eq 0) { gh release upload $tag release\CyberDisk.exe release\CyberDisk.exe.sha256 --clobber --repo $Repo }
    else { gh release create $tag release\CyberDisk.exe release\CyberDisk.exe.sha256 --title "CyberDisk $Version" --notes-file release\notes.md --repo $Repo }
    Write-Host "Done: https://github.com/$Repo/releases/tag/$tag" -ForegroundColor Green
}
else {
    Write-Host "== Last step by hand (the GitHub CLI is not installed) ==" -ForegroundColor Yellow
    Write-Host "The release page opens now. Drag release\CyberDisk.exe (and CyberDisk.exe.sha256) onto it, paste release\notes.md as the description and press Publish."
    Start-Process "https://github.com/$Repo/releases/new?tag=$tag"
    Start-Process explorer.exe (Resolve-Path release)
}
