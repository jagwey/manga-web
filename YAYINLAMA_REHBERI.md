# Kage 95 - Canlı Yayına Alma Rehberi (Deployment Handbook)

Bu rehber, Kage 95 manga sitenizi bir alan adı (domain) satın alıp internet üzerinde 7/24 yayına almanızı sağlayacak tüm adımları adım adım anlatır.

---

## 1. Adım: Alan Adı (Domain) Satın Alma

1. İstediğiniz bir domain kayıt firmasından (örn. [Namecheap](https://www.namecheap.com/), [Cloudflare Registrar](https://www.cloudflare.com/products/registrar/) veya [Turhost](https://www.turhost.com/)) bir alan adı satın alın (Örn: `mangasiteniz.com`).
2. Yıllık maliyet genelde **300 - 450 TL** (~10-12$) civarındadır.

---

## 2. Adım: Sunucu (VPS) Kiralama

> **Önemli Hatırlatma:** Klasik cPanel/PHP paylaşımlı hosting bu siteyi çalıştırmaz. Bir adet **Sanal Sunucu (VPS / VDS)** kiralamanız gerekir.

- **Önerilen Firmalar:** [Hetzner](https://www.hetzner.com/) (En popüler ve ucuz, ayda ~4€), [Vultr](https://www.vultr.com/), [DigitalOcean](https://www.digitalocean.com/) veya yerli firmalar (Cenuta, Turhost, Güzel Hosting).
- **Önerilen Sunucu Özellikleri:**
  - **İşletim Sistemi:** Ubuntu 24.04 LTS (veya 22.04 LTS)
  - **RAM:** En az 2 GB (Manga görselleri ve SQL Server için 4 GB çok daha rahattır)
  - **CPU:** 2 vCPU
  - **Disk:** 40-50 GB SSD / NVMe

Sunucuyu kiraladığınızda size bir **IP Adresi** (Örn: `159.65.120.45`) ve `root` şifresi verilecektir.

---

## 3. Adım: Alan Adını Sunucuya Bağlama (DNS A Kaydı)

Alan adını aldığınız panelde (Namecheap / Cloudflare / Turhost) **DNS Yönetimi** sayfasına gidin:

1. **A Kaydı 1:**
   - **Ad / Host:** `@`
   - **Değer / IP:** Sunucunuzun IP adresi (örn. `159.65.120.45`)
2. **A Kaydı 2:**
   - **Ad / Host:** `www`
   - **Değer / IP:** Sunucunuzun IP adresi (örn. `159.65.120.45`)

*(DNS yönlendirmesinin dünyada yayılması 5 ila 30 dakika sürebilir).*

---

## 4. Adım: Sunucuda Kurulum (2 Farklı Kolay Yol)

Sunucunuza bilgisayarınızdaki PowerShell veya Terminal'den SSH ile bağlanın:
```bash
ssh root@SUNUCU_IP_ADRESINIZ
```

Aşağıdaki iki kurulum yolundan birini seçebilirsiniz:

### YOL A: Docker ile Tek Komutta Kurulum (En Kolay Yol — Önerilen)

Tüm bağımlılıklar (SQL Server, .NET 10, Nginx) hazır konteynerler halinde çalışır:

1. Sunucuya Docker ve Git kurun:
   ```bash
   apt update && apt upgrade -y
   apt install -y git docker.io docker-compose-v2
   ```

2. Proje dosyalarınızı sunucuya çekin veya SCP ile yükleyin:
   ```bash
   git clone <PROJE_REPO_ADRESINIZ> /var/www/kage95
   cd /var/www/kage95
   ```

3. `docker-compose.yml` içindeki `JwtSettings__Secret` alanına en az 32 karakterlik rastgele bir şifre yazın.

4. Sitenizi tek komutla başlatın:
   ```bash
   docker compose up -d --build
   ```

5. Veritabanı tablolarını oluşturmak için:
   ```bash
   docker exec -i kage95_db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "GucluSifreKage95*!" -C -i /var/www/kage95/Veritabani_Kurulumu.sql
   ```

Artık siteniz `http://siteniz.com` adresinde yayında!

---

### YOL B: Klasik Ubuntu Kurulumu (.NET 10 + Nginx + MSSQL Express)

1. **Gerekli Paketleri Kurun:**
   ```bash
   apt update && apt install -y nginx curl certbot python3-certbot-nginx
   ```

2. **.NET 10 SDK ve Runtime Kurun:**
   ```bash
   # Microsoft paket deposunu ekleyin
   add-apt-repository ppa:dotnet/backports -y
   apt update && apt install -y dotnet-sdk-10.0
   ```

3. **Dosyaları Sunucuya Aktarın:**
   - Projenizdeki derlenmiş `publish/backend` klasörünü sunucuda `/var/www/kage95/backend` klasörüne yükleyin.
   - Ön yüz dosyalarını (`index.html`, `read.html`, `login.html`, `admin.html`, `css/`, `js/`) sunucuda `/var/www/kage95/frontend` klasörüne yükleyin.

4. **Arka Plan Servisini (systemd) Başlatın:**
   ```bash
   cp /var/www/kage95/deploy/systemd/kage95.service /etc/systemd/system/
   systemctl daemon-reload
   systemctl enable --now kage95.service
   ```
   *Durumu kontrol edin: `systemctl status kage95.service` (Yeşil active (running) olmalıdır).*

5. **Nginx Ayarlarını Etkinleştirin:**
   ```bash
   cp /var/www/kage95/deploy/nginx/kage95.conf /etc/nginx/sites-available/kage95.conf
   # Dosyadaki 'siteniz.com' yerlerini kendi domaininizle değiştirin:
   nano /etc/nginx/sites-available/kage95.conf

   ln -s /etc/nginx/sites-available/kage95.conf /etc/nginx/sites-enabled/
   rm -f /etc/nginx/sites-enabled/default
   nginx -t && systemctl reload nginx
   ```

---

## 5. Adım: Ücretsiz SSL (HTTPS) Kilidi Alma

Sitenizin `https://` ile güvenli açılması için Let's Encrypt sertifikası alın (tamamen ücretsizdir):

```bash
certbot --nginx -d siteniz.com -d www.siteniz.com
```

- E-posta adresinizi girin ve şartları kabul edin.
- Certbot otomatik olarak Nginx yapılandırmanızı HTTPS'e güncelleyecek ve SSL sertifikanızı süresi doldukça kendisi yenileyecektir.

---

## 6. Adım: İlk Yöneticiyi (Site Sahibini) Tanımlama

1. Sitenize gidin (`https://siteniz.com/login.html`) ve bir hesap oluşturun (Örn: `Jagwey` veya kendi adınız).
2. Veritabanına bağlanıp hesabınıza `Owner` rolü verin:
   ```sql
   UPDATE dbo.Kullanicilar SET Rol = N'Owner' WHERE KullaniciAdi = N'KullaniciAdiniz';
   ```
3. Artık `https://siteniz.com/admin.html` sayfasına girdiğinizde tüm yönetim yetkilerine sahip olacaksınız!

---

## 7. Bakım ve Güncelleme Komutları

- **Backend loglarını canlı izleme:**
  ```bash
  journalctl -u kage95.service -f
  ```
- **Backend'i yeniden başlatma:**
  ```bash
  systemctl restart kage95.service
  ```
- **Nginx'i yeniden başlatma:**
  ```bash
  systemctl restart nginx
  ```
