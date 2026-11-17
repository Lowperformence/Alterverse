param(
    [string]$ServerImage = "registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server:20261116-192555",
    [string]$EdgegapApiToken = $env:EDGEGAP_API_TOKEN
)

$ErrorActionPreference = "Stop"
$RepoBase = "registry.edgegap.com/dimensions-front-yf4x25e6aj2i/dimensions-front-server"
$Tag = ($ServerImage -split ":")[-1]
$VersionTagImage = "${RepoBase}:v0.1.2-server"

Write-Host "=== 1. Lokaler Container-Kurztest ($ServerImage) ===" -ForegroundColor Cyan
$containerId = docker run -d --platform linux/amd64 -p 7777:7777/udp $ServerImage
try {
    Start-Sleep -Seconds 6
    $logs = docker logs $containerId 2>&1 | Out-String
    Write-Host $logs
    if ($logs -notmatch "listening on UDP 7777") {
        Write-Warning "Hinweis: 'listening on UDP 7777' noch nicht im Log gesehen (evtl. laedt der Server noch). Prüfe Ausgabe oben."
    } else {
        Write-Host "OK: Dedicated Server lauscht erfolgreich auf UDP 7777!" -ForegroundColor Green
    }
} finally {
    docker rm -f $containerId | Out-Null
}

Write-Host "`n=== 2. Docker-Image für Edgegap taggen & hochladen ===" -ForegroundColor Cyan
docker tag $ServerImage $VersionTagImage
docker push $ServerImage
if ($LASTEXITCODE -ne 0) {
    throw "docker push fehlgeschlagen. Bitte vorher 'docker login registry.edgegap.com' ausführen!"
}
docker push $VersionTagImage
if ($LASTEXITCODE -ne 0) {
    throw "docker push fuer v0.1.2-server fehlgeschlagen."
}
Write-Host "OK: Beides hochgeladen:`n  - $ServerImage`n  - $VersionTagImage" -ForegroundColor Green

if (-not [string]::IsNullOrWhiteSpace($EdgegapApiToken)) {
    Write-Host "`n=== 3. Aktualisiere Edgegap Application Version per API ===" -ForegroundColor Cyan
    $headers = @{
        "Authorization" = $EdgegapApiToken
        "Content-Type"  = "application/json"
    }
    $patchBody = @{
        docker_repository = "dimensions-front-yf4x25e6aj2i/dimensions-front-server"
        docker_image      = "dimensions-front-server"
        docker_tag        = $Tag
        req_cpu           = 1024
        req_memory        = 1024
        req_video         = 0
        is_active         = $true
        ports             = @(
            @{
                port     = 7777
                protocol = "UDP"
                name     = "gameport"
                tls_upgrade = $false
                to_check = $false
            }
        )
    } | ConvertTo-Json -Depth 6

    try {
        Invoke-RestMethod -Method Patch -Uri "https://api.edgegap.com/v1/app/dimensions-front/version/v0.1.1-server" -Headers $headers -Body $patchBody | Out-Null
        Write-Host "OK: Edgegap Version 'v0.1.1-server' auf Tag '$Tag' umgestellt!" -ForegroundColor Green
    } catch {
        Write-Warning "Konnte v0.1.1-server nicht per PATCH aktualisieren: $($_.Exception.Message)"
    }

    $createBody = @{
        name              = "v0.1.2-server"
        docker_repository = "dimensions-front-yf4x25e6aj2i/dimensions-front-server"
        docker_image      = "dimensions-front-server"
        docker_tag        = $Tag
        private_registry  = $true
        req_cpu           = 1024
        req_memory        = 1024
        req_video         = 0
        is_active         = $true
        ports             = @(
            @{
                port     = 7777
                protocol = "UDP"
                name     = "gameport"
                tls_upgrade = $false
                to_check = $false
            }
        )
    } | ConvertTo-Json -Depth 6

    try {
        Invoke-RestMethod -Method Post -Uri "https://api.edgegap.com/v1/app/dimensions-front/version" -Headers $headers -Body $createBody | Out-Null
        Write-Host "OK: Edgegap Version 'v0.1.2-server' mit Tag '$Tag' neu angelegt!" -ForegroundColor Green
    } catch {
        Invoke-RestMethod -Method Patch -Uri "https://api.edgegap.com/v1/app/dimensions-front/version/v0.1.2-server" -Headers $headers -Body $patchBody | Out-Null
        Write-Host "OK: Bestehende Edgegap Version 'v0.1.2-server' auf Tag '$Tag' aktualisiert!" -ForegroundColor Green
    }
} else {
    Write-Host "`n=== 3. Letzter Schritt im Edgegap-Dashboard ===" -ForegroundColor Yellow
    Write-Host "1. Öffne im Edgegap-Dashboard: Applications -> dimensions-front -> Versions"
    Write-Host "2. Klicke bei der aktiven Version (v0.1.1-server) auf Edit ODER erstelle Version 'v0.1.2-server'"
    Write-Host "3. Wähle als Image/Tag: '$Tag' (oder 'v0.1.2-server'), Port: 7777 UDP (gameport) und speichere."
    Write-Host "4. Falls du 'v0.1.2-server' neu angelegt hast: Unter Matchmaker -> dimensions-front-live die Datei 'Server/matchmaking/edgegap-casual-live-v1.2.json' importieren."
}
