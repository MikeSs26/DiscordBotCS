#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes the bot and deploys it to the droplet over SSH.

.DESCRIPTION
    Builds a self-contained Linux binary (the droplet only has the .NET 8 runtime
    and this project targets net10.0, so self-contained is required), ships it as
    a single tarball, and restarts the systemd service.

    The database is NOT touched: it lives in /var/lib/discordbot, outside the
    directory this script replaces.

.EXAMPLE
    .\deploy\deploy.ps1 -ServerHost 24.144.98.61
#>
[CmdletBinding()]
param(
    # Droplet IP or hostname.
    [string]$ServerHost = '24.144.98.61',

    # SSH user with sudo rights (not the service account).
    [string]$SshUser = 'root',

    # Where the app binaries live on the server.
    [string]$RemoteDir = '/opt/discordbot',

    # systemd unit name.
    [string]$ServiceName = 'discordbot',

    # Use 'linux-arm64' if the droplet is ARM.
    [string]$Runtime = 'linux-x64'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir  = Join-Path $projectRoot 'publish'
$tarball     = Join-Path $projectRoot 'publish.tar.gz'
$target      = "$SshUser@$ServerHost"

# Sends a script to the server as base64. Piping the text directly is not safe:
# PowerShell prepends a UTF-8 BOM and uses CRLF, both of which break bash.
function Invoke-Remote {
    param([Parameter(Mandatory)][string]$Script)
    $clean = $Script -replace "`r`n", "`n"
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($clean))
    ssh -o BatchMode=yes $target "echo $b64 | base64 -d | bash"
}

Write-Host "==> Publishing ($Runtime, self-contained)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

# Trimming and AOT are deliberately OFF: Discord.Net discovers command modules
# by reflection, which a trimmer would strip.
dotnet publish (Join-Path $projectRoot 'DiscordBotCS.csproj') `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishTrimmed=false `
    -p:PublishSingleFile=false `
    --output $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Write-Host "==> Packing..." -ForegroundColor Cyan
if (Test-Path $tarball) { Remove-Item $tarball -Force }
tar -czf $tarball -C $publishDir .
if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE." }
Write-Host ("    {0:N1} MB" -f ((Get-Item $tarball).Length / 1MB))

Write-Host "==> Uploading to $target..." -ForegroundColor Cyan
scp -o BatchMode=yes $tarball "${target}:/tmp/discordbot-publish.tar.gz"
if ($LASTEXITCODE -ne 0) { throw "scp failed with exit code $LASTEXITCODE." }

Write-Host "==> Installing and restarting service..." -ForegroundColor Cyan
Invoke-Remote @"
set -euo pipefail

sudo systemctl stop $ServiceName || true
sudo mkdir -p $RemoteDir
sudo find $RemoteDir -mindepth 1 -delete
sudo tar -xzf /tmp/discordbot-publish.tar.gz -C $RemoteDir
sudo chown -R discordbot:discordbot $RemoteDir

# tar built on Windows carries no Unix permissions, so everything lands as 777.
# Reset to sane modes: only the apphost is executable, nothing is world-writable.
sudo find $RemoteDir -type d -exec chmod 755 {} +
sudo find $RemoteDir -type f -exec chmod 644 {} +
sudo chmod 755 $RemoteDir/DiscordBotCS
rm -f /tmp/discordbot-publish.tar.gz

# Marker so we only read log lines produced by this start.
STARTED="`$(date '+%Y-%m-%d %H:%M:%S')"
sudo systemctl start $ServiceName

# 'active' alone is a false green: with a bad token the host stays up but idle.
# Wait for the gateway handshake the bot logs on success.
for i in `$(seq 1 40); do
  LOG="`$(sudo journalctl -u $ServiceName --since "`$STARTED" --no-pager 2>/dev/null || true)"
  if echo "`$LOG" | grep -q 'Connected as'; then
    echo "OK: `$(echo "`$LOG" | grep -m1 'Connected as' | sed 's/.*Connected as/Connected as/')"
    exit 0
  fi
  if echo "`$LOG" | grep -q 'No Discord token configured'; then
    echo 'FALLO: falta el token en /etc/discordbot/discordbot.env' >&2
    exit 1
  fi
  if echo "`$LOG" | grep -q '401: Unauthorized'; then
    echo 'FALLO: Discord rechazo el token (401). Revisa /etc/discordbot/discordbot.env' >&2
    sudo systemctl stop $ServiceName
    exit 1
  fi
  if ! sudo systemctl is-active --quiet $ServiceName; then
    echo 'FALLO: el servicio se detuvo.' >&2
    sudo journalctl -u $ServiceName -n 40 --no-pager >&2
    exit 1
  fi
  sleep 1
done

echo 'FALLO: sin confirmacion de conexion tras 40s.' >&2
sudo journalctl -u $ServiceName -n 40 --no-pager >&2
exit 1
"@
if ($LASTEXITCODE -ne 0) { throw "Remote deployment failed with exit code $LASTEXITCODE." }

Remove-Item $tarball -Force

Write-Host ""
Write-Host "Desplegado y conectado. Logs en vivo:" -ForegroundColor Green
Write-Host "  ssh $target 'journalctl -u $ServiceName -f'"
