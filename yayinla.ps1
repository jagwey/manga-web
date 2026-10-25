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

Write-Host "[3/4] On yuz dosyalari derleniyor ve yukleniyor..."
$derleScript = Join-Path $kokDizin "deploy\araclar\derle.mjs"
if (Test-Path $derleScript) {
    Write-Host "  -> derle.mjs calistiriliyor (dist uretiliyor)..."
    node $derleScript
}

$htmlDosyalar = @("index.html", "read.html", "admin.html", "login.html")
foreach ($f in $htmlDosyalar) {
    $distYol = Join-Path $kokDizin "dist\$f"
    $tamYol = if (Test-Path $distYol) { $distYol } else { Join-Path $kokDizin $f }
    Write-Host "  -> wwwroot/$f ($tamYol)"
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T $tamYol "$ftpKok/wwwroot/$f"
}

$digerKokDosyalar = @("manifest.webmanifest", "sw.js")
foreach ($f in $digerKokDosyalar) {
    $tamYol = Join-Path $kokDizin $f
    if (Test-Path $tamYol) {
        Write-Host "  -> wwwroot/$f"
        curl.exe -s -S --ssl-reqd -k -u $kimlik -T $tamYol "$ftpKok/wwwroot/$f"
    }
}

Get-ChildItem (Join-Path $kokDizin "assets") -File | ForEach-Object {
    Write-Host "  -> wwwroot/assets/$($_.Name)"
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T $_.FullName "$ftpKok/wwwroot/assets/$($_.Name)"
}
# Tarayici varsayilan kok favicon.ico ve favicon.svg geriye donuk uyumluluk
if (Test-Path (Join-Path $kokDizin "assets\favicon.ico")) {
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T (Join-Path $kokDizin "assets\favicon.ico") "$ftpKok/wwwroot/favicon.ico"
}
if (Test-Path (Join-Path $kokDizin "assets\favicon.svg")) {
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T (Join-Path $kokDizin "assets\favicon.svg") "$ftpKok/wwwroot/favicon.svg"
}

Get-ChildItem (Join-Path $kokDizin "css") -File | ForEach-Object {
    Write-Host "  -> wwwroot/css/$($_.Name)"
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T $_.FullName "$ftpKok/wwwroot/css/$($_.Name)"
}

Get-ChildItem (Join-Path $kokDizin "js") -File | ForEach-Object {
    Write-Host "  -> wwwroot/js/$($_.Name)"
    curl.exe -s -S --ssl-reqd -k -u $kimlik -T $_.FullName "$ftpKok/wwwroot/js/$($_.Name)"
}

$distJs = Join-Path $kokDizin "dist\js"
if (Test-Path $distJs) {
    Get-ChildItem $distJs -File | ForEach-Object {
        Write-Host "  -> wwwroot/js/$($_.Name) [dist]"
        curl.exe -s -S --ssl-reqd -k -u $kimlik -T $_.FullName "$ftpKok/wwwroot/js/$($_.Name)"
    }
}

if (-not $SadeceOnyuz) {
    curl.exe -s -S --ssl-reqd -k -u $kimlik "$ftpKok/" -Q "-DELE app_offline.htm" | Out-Null
}

Write-Host "[4/4] Canli site kontrol ediliyor ($($j.siteAdresi))..."
$kod = curl.exe -s -o NUL -w "%{http_code}" "$($j.siteAdresi)/"
Write-Host "Tamamlandi! $($j.siteAdresi)/ durum kodu: $kod"
