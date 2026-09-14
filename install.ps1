param([string]$ModsPath = $env:CSII_LOCALMODSPATH)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ModsPath)) { throw 'CSII_LOCALMODSPATH is missing. Set -ModsPath to the game Mods folder.' }
$source = Join-Path $PSScriptRoot 'local\CopyIt'
$root = [IO.Path]::GetFullPath($ModsPath)
$target = [IO.Path]::GetFullPath((Join-Path $root 'CopyIt'))
if ([IO.Path]::GetDirectoryName($target) -ne $root.TrimEnd('\')) { throw 'Invalid target path.' }
foreach ($required in @('CopyIt.dll','CopyIt.mjs','CopyIt.css','images\CopyIt\copy.svg')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required))) { throw "Missing package file: $required" }
}
if (Get-Process Cities2 -ErrorAction SilentlyContinue) { throw 'Close Cities: Skylines II before installing Copy It.' }
if (Test-Path -LiteralPath $target) {
    $backup = Join-Path $PSScriptRoot ('installation-backups\CopyIt-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination $backup -Recurse
    Write-Output "Previous Copy It backed up: $backup"
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-ChildItem -LiteralPath $source -File -Recurse | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($source,$_.FullName)
    $destination = Join-Path $target $relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Hash mismatch: $relative" }
}
Write-Output "Copy It installed and hashes verified: $target"
