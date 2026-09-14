$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Path .\research -Force | Out-Null
    dotnet build .\src\CopyIt.csproj -c Release --nologo *> .\research\build.log
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed. See research/build.log.' }
    dotnet run --project .\tests\CopyIt.Tests.csproj -c Release *> .\research\tests.log
    if ($LASTEXITCODE -ne 0) { throw 'Policy tests failed. See research/tests.log.' }
    dotnet run --project .\slope-tests\SlopeTests.csproj -c Release *> .\research\slope-tests.log
    if ($LASTEXITCODE -ne 0) { throw 'Decal slope tests failed.' }
    Push-Location ui
    try {
        npm.cmd ci --no-audit --no-fund *> ..\research\npm-install.log
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        npm.cmd run build *> ..\research\ui-build.log
        if ($LASTEXITCODE -ne 0) { throw 'UI build failed.' }
    } finally { Pop-Location }
    node .\tests\ui-contract.cjs *> .\research\ui-tests.log
    if ($LASTEXITCODE -ne 0) { throw 'UI contract tests failed.' }
    Get-Content .\research\build.log -Tail 5
    Get-Content .\research\tests.log
} finally { Pop-Location }
