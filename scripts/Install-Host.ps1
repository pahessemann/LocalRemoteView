#Requires -RunAsAdministrator
param([string]$InstallDirectory = "$env:ProgramFiles\LocalRemoteView")
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '..\publish\Host'
if (-not (Test-Path (Join-Path $source 'LocalRemoteView.Host.exe'))) { throw 'Publiez d’abord le projet avec scripts\Publish.ps1.' }
New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $InstallDirectory -Recurse -Force
$exe = Join-Path $InstallDirectory 'LocalRemoteView.Host.exe'
$action = New-ScheduledTaskAction -Execute $exe
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName 'LocalRemoteView Host' -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'Agent local de partage et contrôle LocalRemoteView' -Force | Out-Null
if (-not (Get-NetFirewallRule -DisplayName 'LocalRemoteView (LAN)' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'LocalRemoteView (LAN)' -Direction Inbound -Action Allow -Program $exe -Protocol TCP -Profile Private -RemoteAddress LocalSubnet | Out-Null
}
Start-Process -FilePath $exe
Write-Host 'Installation terminée. La fenêtre de configuration initiale va s’ouvrir une seule fois.' -ForegroundColor Green
