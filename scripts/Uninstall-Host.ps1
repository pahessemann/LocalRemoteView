#Requires -RunAsAdministrator
param([string]$InstallDirectory = "$env:ProgramFiles\LocalRemoteView", [switch]$RemoveConfiguration)
$ErrorActionPreference = 'Stop'
Unregister-ScheduledTask -TaskName 'LocalRemoteView Host' -Confirm:$false -ErrorAction SilentlyContinue
Get-Process 'LocalRemoteView.Host' -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-NetFirewallRule -DisplayName 'LocalRemoteView (LAN)' -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $InstallDirectory) { Remove-Item -LiteralPath $InstallDirectory -Recurse -Force }
if ($RemoveConfiguration) {
    $config = Join-Path $env:LOCALAPPDATA 'LocalRemoteView'
    if (Test-Path -LiteralPath $config) { Remove-Item -LiteralPath $config -Recurse -Force }
}
Write-Host 'LocalRemoteView a été désinstallé.' -ForegroundColor Green
