# Builds everything that goes into a release, into dist/:
#   DUN-Setup-<version>.exe     installer (Inno Setup 6 required)
#   DUN-<version>-portable.exe  the app as a single self-contained exe
#   DUN-<version>-src.zip       the source tree
param([string]$Version = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'DUN.csproj'))).Project.PropertyGroup.Version | Select-Object -First 1
}
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist 'publish'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }

Write-Host "== Publishing DUN $Version"
dotnet publish (Join-Path $root 'DUN.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:Version=$Version -o $publish
if ($LASTEXITCODE) { throw 'dotnet publish failed' }
Copy-Item (Join-Path $publish 'DUN.exe') (Join-Path $dist "DUN-$Version-portable.exe")

Write-Host "== Building installer"
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }
& $iscc /Q "/DAppVersion=$Version" "/DPublishDir=$publish" (Join-Path $root 'installer\DUN.iss')
if ($LASTEXITCODE) { throw 'Inno Setup failed' }

Write-Host "== Packing source"
$src = Join-Path $dist "DUN-$Version-src"
New-Item -ItemType Directory $src | Out-Null
foreach ($item in 'src', 'assets', 'docs', 'scripts', 'installer', 'DUN.csproj', 'README.md', 'LICENSE', '.gitignore') {
    Copy-Item (Join-Path $root $item) $src -Recurse
}
Compress-Archive -Path $src -DestinationPath (Join-Path $dist "DUN-$Version-src.zip")
Remove-Item $src -Recurse -Force

Write-Host "== Done"
Get-ChildItem $dist -File | Format-Table Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
