# Builds everything that goes into a release, into dist/:
#   DUN-Setup-<version>.exe     installer: the window in installer/shell with the Inno Setup engine inside
#   DUN-<version>-portable.exe  the app as a single self-contained exe
#   DUN-<version>-src.zip       the source tree
# Needs the .NET 8 SDK and Inno Setup 6. The installer window is compiled with the .NET Framework
# compiler that comes with Windows.
param([string]$Version = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'DUN.csproj'))).Project.PropertyGroup.Version | Select-Object -First 1
}
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist 'publish'
$engineDir = Join-Path $dist 'engine'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }

Write-Host "== Publishing DUN $Version"
dotnet publish (Join-Path $root 'DUN.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:Version=$Version -o $publish
if ($LASTEXITCODE) { throw 'dotnet publish failed' }
Copy-Item (Join-Path $publish 'DUN.exe') (Join-Path $dist "DUN-$Version-portable.exe")

Write-Host "== Building install engine"
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }
& $iscc /Q "/DAppVersion=$Version" "/DPublishDir=$publish" (Join-Path $root 'installer\DUN.iss')
if ($LASTEXITCODE) { throw 'Inno Setup failed' }

Write-Host "== Building installer window"
$shell = Join-Path $root 'installer\shell'
$info = Join-Path $engineDir 'AssemblyInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("DUN Setup")]
[assembly: AssemblyProduct("DUN")]
[assembly: AssemblyCompany("Sandun")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Sandun")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
"@ | Set-Content -LiteralPath $info -Encoding UTF8
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$icon = Join-Path $root 'assets\DUN.ico'
& (Join-Path $fx 'csc.exe') /nologo /target:winexe /platform:anycpu /optimize+ `
    "/out:$(Join-Path $dist "DUN-Setup-$Version.exe")" "/win32icon:$icon" "/win32manifest:$(Join-Path $shell 'app.manifest')" `
    "/resource:$(Join-Path $shell 'SetupWindow.xaml'),Dun.Setup.Window" "/resource:$icon,Dun.Setup.Icon" `
    "/resource:$(Join-Path $engineDir 'DUN-Engine.exe'),Dun.Setup.Engine" `
    "/reference:$fx\WPF\WindowsBase.dll" "/reference:$fx\WPF\PresentationCore.dll" "/reference:$fx\WPF\PresentationFramework.dll" `
    /reference:System.Xaml.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll `
    (Join-Path $shell 'Setup.cs') $info
if ($LASTEXITCODE) { throw 'Installer window failed to compile' }
Remove-Item $engineDir, $publish -Recurse -Force

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
