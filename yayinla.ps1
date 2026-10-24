param(
    [switch]$SadeceOnyuz
)

$ErrorActionPreference = "Stop"
$kokDizin = Split-Path -Parent $MyInvocation.MyCommand.Path
$ayarDosyasi = Join-Path $kokDizin "deploy\yayin.Local.json"

if (-not (Test-Path $ayarDosyasi)) {
    Write-Error "deploy\yayin.Local.json bulunamadi!"
    exit 1
}

$j = Get-Content $ayarDosyasi -Raw | ConvertFrom-Json
$kimlik = "$($j.ftpKullanici):$($j.ftpSifre)"
$ftpKok = "ftp://$($j.ftpSunucu)$($j.ftpKlasor)"

if (-not $SadeceOnyuz) {
    Write-Host "[1/4] Backend Release olarak derleniyor..."
    $pubDir = Join-Path $env:TEMP "kage95_pub"
    if (Test-Path $pubDir) { Remove-Item $pubDir -Recurse -Force }
    dotnet publish (Join-Path $kokDizin "backend\backend.csproj") -c Release -o $pubDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish basarisiz oldu." }

    Write-Host "[2/4] IIS kilidini acmak icin app_offline.htm gonderiliyor..."
    $offFile = Join-Path $pubDir "app_offline.htm"
    "Bakim" | Set-Content $offFile -Encoding utf8
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T $offFile "$ftpKok/app_offline.htm"
    Start-Sleep -Seconds 3

    foreach ($f in @("backend.dll", "backend.pdb", "backend.exe", "backend.deps.json")) {
        Write-Host "  -> $f yukleniyor..."
        curl.exe -s -S --ssl-reqd -k -u $kimlik -T (Join-Path $pubDir $f) "$ftpKok/$f"
    }
} else {
    Write-Host "[1/2] Sadece on yuz (HTML/CSS/JS) guncelleniyor..."
}

Write-Host "[3/4] On yuz dosyalari (wwwroot) yukleniyor..."
$kokDosyalar = @("index.html", "read.html", "admin.html", "login.html", "favicon.svg", "manifest.webmanifest", "sw.js", "icon-192.png", "icon-512.png")
foreach ($f in $kokDosyalar) {
    $tamYol = Join-Path $kokDizin $f
    if (Test-Path $tamYol) {
        Write-Host "  -> wwwroot/$f"
        curl.exe -s -S --ssl-reqd -k -u $kimlik -T $tamYol "$ftpKok/wwwroot/$f"
    }
}

Get-ChildItem (Join-Path $kokDizin "css") -File | ForEach-Object {
    Write-Host "  -> wwwroot/css/$($_.Name)"
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T $_.FullName "$ftpKok/wwwroot/css/$($_.Name)"
}

Get-ChildItem (Join-Path $kokDizin "js") -File | ForEach-Object {
    Write-Host "  -> wwwroot/js/$($_.Name)"
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T $_.FullName "$ftpKok/wwwroot/js/$($_.Name)"
}

if (-not $SadeceOnyuz) {
    curl.exe -s -S --ssl-reqd -k -u $kimlik "$ftpKok/" -Q "DELE $($j.ftpKlasor)/app_offline.htm" | Out-Null
}

Write-Host "[4/4] Canli site kontrol ediliyor ($($j.siteAdresi))..."
$kod = curl.exe -s -o NUL -w "%{http_code}" "$($j.siteAdresi)/"
Write-Host "Tamamlandi! $($j.siteAdresi)/ durum kodu: $kod"
