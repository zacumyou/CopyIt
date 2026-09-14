$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$stage = Join-Path $root 'package\CopyIt-0.5.6'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($file in @('README.ko.md','TESTING.ko.md','NOTICE.md','build.ps1','install.ps1','package.ps1')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage -Force
}
foreach ($directory in @('src','tests','slope-tests','ui','local\CopyIt','research')) { New-Item -ItemType Directory -Path (Join-Path $stage $directory) -Force | Out-Null }
Get-ChildItem -LiteralPath (Join-Path $root 'src') -File | Copy-Item -Destination (Join-Path $stage 'src') -Force
Get-ChildItem -LiteralPath (Join-Path $root 'tests') -File | Copy-Item -Destination (Join-Path $stage 'tests') -Force
Get-ChildItem -LiteralPath (Join-Path $root 'slope-tests') -File | Copy-Item -Destination (Join-Path $stage 'slope-tests') -Force
foreach ($directory in @('src','types','tools')) { Copy-Item -LiteralPath (Join-Path $root "ui\$directory") -Destination (Join-Path $stage 'ui') -Recurse -Force }
Get-ChildItem -LiteralPath (Join-Path $root 'ui') -File | Copy-Item -Destination (Join-Path $stage 'ui') -Force
foreach ($file in @('CopyIt.dll','CopyIt.pdb','CopyIt_win_x86_64.dll','CopyIt_win_x86_64.pdb','CopyIt.mjs','CopyIt.css','CopyIt.mjs.LICENSE.txt','NOTICE.md')) {
    Copy-Item -LiteralPath (Join-Path $root "local\CopyIt\$file") -Destination (Join-Path $stage 'local\CopyIt') -Force
}
$images = Join-Path $stage 'local\CopyIt\images\CopyIt'
New-Item -ItemType Directory -Path $images -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'local\CopyIt\images\CopyIt') -Filter '*.svg' -File | Copy-Item -Destination $images -Force
foreach ($file in @('build.log','tests.log','ui-build.log','ui-tests.log')) { Copy-Item -LiteralPath (Join-Path $root "research\$file") -Destination (Join-Path $stage 'research') -Force }
$manifest = Get-ChildItem -LiteralPath (Join-Path $stage 'local\CopyIt') -File -Recurse | ForEach-Object {
    [pscustomobject]@{File=[IO.Path]::GetRelativePath($stage,$_.FullName);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'SHA256.json') -Encoding utf8
$zip = Join-Path $root 'CopyIt-0.5.6.zip'
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force
Write-Output $zip







