$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
$artifacts = Join-Path $root 'artifacts'
$delivery = Join-Path $root 'Livraison'
New-Item -ItemType Directory -Force -Path $artifacts, $delivery | Out-Null
& $dotnet publish (Join-Path $root 'src\LocalRemoteView.Host\LocalRemoteView.Host.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $artifacts
if ($LASTEXITCODE -ne 0) { throw 'La publication de l’hôte a échoué.' }
Copy-Item -LiteralPath (Join-Path $artifacts 'LocalRemoteView.Host.exe') -Destination (Join-Path $artifacts 'HostPayload.exe') -Force
& $dotnet publish (Join-Path $root 'src\LocalRemoteView.Installer\LocalRemoteView.Installer.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $artifacts
if ($LASTEXITCODE -ne 0) { throw 'La publication de l’installateur a échoué.' }
& $dotnet publish (Join-Path $root 'src\LocalRemoteView.Client\LocalRemoteView.Client.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $artifacts
if ($LASTEXITCODE -ne 0) { throw 'La publication du client a échoué.' }
Copy-Item -LiteralPath (Join-Path $artifacts 'LocalRemoteView-Installer.exe') -Destination (Join-Path $delivery 'LocalRemoteView-Installer.exe') -Force
Copy-Item -LiteralPath (Join-Path $artifacts 'LocalRemoteView.Client.exe') -Destination (Join-Path $delivery 'LocalRemoteView.exe') -Force
Write-Host "Les deux fichiers sont prêts dans $delivery" -ForegroundColor Green
