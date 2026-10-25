using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    /// <summary>Giriş yapmış kullanıcının kendi hesabı.</summary>
    [ApiController]
    [Route("api/hesap")]
    [Authorize]
    public class HesapController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IDenetimServisi _denetim;
        private readonly IGirisSinirlayici _sinirlayici;
        private readonly IWebHostEnvironment _env;
        private readonly TercihServisi _tercihler;
        private readonly IslemSinirlayici _hizSinir;

        public HesapController(AppDbContext context, ITokenService tokenService, IDenetimServisi denetim,
            IGirisSinirlayici sinirlayici, IWebHostEnvironment env, TercihServisi tercihler, IslemSinirlayici hizSinir)
        {
            _tercihler = tercihler;
            _context = context;
            _tokenService = tokenService;
            _denetim = denetim;
            _sinirlayici = sinirlayici;
            _env = env;
            _hizSinir = hizSinir;
        }

        private async Task<Kullanici?> MevcutAsync()
        {
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idStr, out var id)) return null;
            return await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Id == id);
        }

        /// <summary>Mevcut şifreyi doğrular; yanlış denemeler giriş sınırlayıcıya sayılır.</summary>
        private IActionResult? SifreKontrol(Kullanici k, string? sifre)
        {
            var anahtar = "hesap:" + k.Id;
            var bekleme = _sinirlayici.Engelli(anahtar);
            if (bekleme != null)
            {
                var dk = Math.Max(1, (int)Math.Ceiling(bekleme.Value.TotalMinutes));
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = $"Çok fazla hatalı şifre denemesi. Lütfen yaklaşık {dk} dakika sonra tekrar deneyin." });
            }
            if (string.IsNullOrEmpty(sifre) || !HesapKurallari.SifreDogru(k, sifre, out _))
            {
                _sinirlayici.HataKaydet(anahtar);
                return BadRequest(new { message = "Mevcut şifre hatalı." });
            }
            _sinirlayici.Sifirla(anahtar);
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> Getir()
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });
            await PuanServisi.GunlukGirisAsync(_context, k.Id); // günlük giriş puanı (günde bir kez)
            return Ok(new
            {
                id = k.Id,
                kullaniciAdi = k.KullaniciAdi,
                email = k.Email,
                rol = k.Rol,
                yetkiler = Izinler.Cozumle(k),
                profilResmiURL = k.ProfilResmiURL,
                kayitTarihi = k.KayitTarihi
            });
        }

        /// <summary>Kullanıcının puan özeti, son hareketleri ve puan kuralları.</summary>
        [HttpGet("puan")]
        public async Task<IActionResult> PuanOzeti()
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });
            await PuanServisi.GunlukGirisAsync(_context, k.Id);

            var kayitlar = _context.PuanKayitlari.AsNoTracking().Where(p => p.KullaniciId == k.Id && p.Puan > 0);
            var toplam = await kayitlar.SumAsync(p => (int?)p.Puan) ?? 0;
            var bas = PuanServisi.BugunBaslangicUtc();
            var bugun = await kayitlar.Where(p => p.Tarih >= bas && p.Eylem != PuanServisi.YoneticiBonus).SumAsync(p => (int?)p.Puan) ?? 0;
            var gecmis = (await kayitlar.OrderByDescending(p => p.Tarih).ThenByDescending(p => p.Id).Take(30)
                .Select(p => new { p.Eylem, p.Puan, p.Tarih }).ToListAsync())
                .Select(p => new { eylem = p.Eylem, aciklama = PuanServisi.Aciklama(p.Eylem), puan = p.Puan, tarih = p.Tarih })
                .ToList();
            var (sonraki, kalan) = PuanServisi.SonrakiRutbe(toplam);

            return Ok(new
            {
                toplam,
                rutbe = PuanServisi.Rutbe(toplam),
                seviye = PuanServisi.Seviye(toplam),
                seviyeIcinGereken = PuanServisi.SeviyeIcinGereken(PuanServisi.Seviye(toplam)),
                sonrakiSeviyeIcinKalan = PuanServisi.SonrakiSeviyeIcinKalan(toplam),
                sonrakiRutbe = sonraki,
                kalan,
                bugun = new { kazanilan = bugun, tavan = PuanServisi.GunlukTavan },
                gecmis,
                kurallar = PuanServisi.KuralListesi()
            });
        }

        // ---------- Seviye ödülleri: kozmetik tercihler ----------

        private async Task<object> TercihYanitiAsync(int kullaniciId, int toplam)
        {
            var seviye = PuanServisi.Seviye(toplam);
            var kayit = await _context.KullaniciTercihleri.AsNoTracking().FirstOrDefaultAsync(t => t.KullaniciId == kullaniciId);
            var gecerli = kayit == null ? null : TercihServisi.Suz(kayit, seviye);
            return new
            {
                seviye,
                tercihler = gecerli ?? new AdStiliDto(),
                acik = new
                {
                    renkler = OdulKatalogu.Renkler.Where(r => r.Seviye <= seviye).Select(r => r.Id).ToList(),
                    fontlar = OdulKatalogu.Fontlar.Where(f => f.Seviye <= seviye).Select(f => f.Id).ToList(),
                    stickerPaketleri = OdulKatalogu.StickerPaketleri.Where(p => p.Seviye <= seviye).Select(p => p.Id).ToList(),
                    efektler = OdulKatalogu.Efektler.Where(e => e.Seviye <= seviye).Select(e => e.Id).ToList(),
                    unvanlar = OdulKatalogu.Unvanlar.Where(u => u.Seviye <= seviye).Select(u => u.Ad).ToList(),
                    ozelRenk = seviye >= OdulKatalogu.OzelRenkSeviye,
                    rozet = seviye >= OdulKatalogu.RozetSeviye,
                    unvan = seviye >= OdulKatalogu.UnvanSeviye
                }
            };
        }

        [HttpGet("tercihler")]
        public async Task<IActionResult> TercihleriGetir()
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });
            return Ok(await TercihYanitiAsync(k.Id, await _tercihler.ToplamPuanAsync(k.Id)));
        }

        [HttpPut("tercihler")]
        public async Task<IActionResult> TercihleriKaydet([FromBody] TercihGuncelleRequest? istek)
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });
            if (!_hizSinir.Dene($"tercih:{k.Id}", 20, TimeSpan.FromMinutes(1)))
                return StatusCode(429, new { message = "Çok sık işlem yapıyorsunuz, lütfen biraz bekleyin." });
            if (istek == null) return BadRequest(new { message = "Geçersiz istek." });
            var toplam = await _tercihler.ToplamPuanAsync(k.Id);
            var seviye = PuanServisi.Seviye(toplam);

            string? adRengi = string.IsNullOrWhiteSpace(istek.AdRengi) ? null : istek.AdRengi.Trim();
            if (adRengi != null)
            {
                var r = OdulKatalogu.Renk(adRengi);
                if (r != null)
                {
                    if (seviye < r.Seviye) return BadRequest(new { message = $"Bu ad rengi için seviye {r.Seviye} gerekir." });
                }
                else if (adRengi.StartsWith('#'))
                {
                    if (seviye < OdulKatalogu.OzelRenkSeviye) return BadRequest(new { message = $"Kendi rengini seçmek için seviye {OdulKatalogu.OzelRenkSeviye} gerekir." });
                    adRengi = OdulKatalogu.OzelRenk(adRengi);
                    if (adRengi == null) return BadRequest(new { message = "Bu renk çok koyu ya da çok açık, adın okunmaz. Biraz daha canlı bir ton seç." });
                }
                else return BadRequest(new { message = "Bilinmeyen ad rengi." });
            }
            string? font = string.IsNullOrWhiteSpace(istek.MesajFontu) ? null : istek.MesajFontu.Trim();
            if (font != null)
            {
                var f = OdulKatalogu.Font(font);
                if (f == null) return BadRequest(new { message = "Bilinmeyen yazı tipi." });
                if (seviye < f.Seviye) return BadRequest(new { message = $"Bu yazı tipi için seviye {f.Seviye} gerekir." });
            }
            string? efekt = string.IsNullOrWhiteSpace(istek.AdEfekti) ? null : istek.AdEfekti.Trim();
            if (efekt != null)
            {
                var ef = OdulKatalogu.Efekt(efekt);
                if (ef == null) return BadRequest(new { message = "Bilinmeyen ad efekti." });
                if (seviye < ef.Seviye) return BadRequest(new { message = $"Bu ad efekti için seviye {ef.Seviye} gerekir." });
            }
            var rozet = istek.Rozet == true;
            if (rozet && seviye < OdulKatalogu.RozetSeviye) return BadRequest(new { message = $"Mühür çerçevesi için seviye {OdulKatalogu.RozetSeviye} gerekir." });

            string? unvan = null;
            if (!string.IsNullOrWhiteSpace(istek.Unvan))
            {
                var hazir = OdulKatalogu.HazirUnvan(istek.Unvan);
                var gereken = OdulKatalogu.UnvanGerekenSeviye(istek.Unvan);
                if (seviye < gereken)
                    return BadRequest(new { message = hazir != null ? $"Bu unvan için seviye {gereken} gerekir." : $"Özel unvan için seviye {gereken} gerekir." });
                if (hazir == null)
                {
                    var hata = OdulKatalogu.UnvanHatasi(istek.Unvan);
                    if (hata != null) return BadRequest(new { message = hata });
                }
                unvan = istek.Unvan.Trim();
            }

            var kayit = await _context.KullaniciTercihleri.FirstOrDefaultAsync(t => t.KullaniciId == k.Id);
            if (kayit == null) { kayit = new KullaniciTercihi { KullaniciId = k.Id }; _context.KullaniciTercihleri.Add(kayit); }
            // Eski Parlak/Animasyon alanlari artik AdEfekti ile tutulur
            kayit.AdRengi = adRengi; kayit.MesajFontu = font; kayit.AdEfekti = efekt;
            kayit.Parlak = false; kayit.Animasyon = false; kayit.Rozet = rozet; kayit.Unvan = unvan;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException) { return Conflict(new { message = "Tercihler kaydedilemedi, lütfen tekrar deneyin." }); }
            _tercihler.Unut(k.Id);
            return Ok(await TercihYanitiAsync(k.Id, toplam));
        }

        [HttpPut("profil")]
        public async Task<IActionResult> ProfilGuncelle([FromBody] ProfilGuncelleRequest request)
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });

            var sifreHata = SifreKontrol(k, request.MevcutSifre);
            if (sifreHata != null) return sifreHata;

            var degisiklikler = new List<string>();
            if (request.KullaniciAdi != null)
            {
                var ad = request.KullaniciAdi.Trim();
                var hata = HesapKurallari.KullaniciAdiHata(ad);
                if (hata != null) return BadRequest(new { message = hata });
                if (!string.Equals(ad, k.KullaniciAdi, StringComparison.Ordinal))
                {
                    if (await _context.Kullanicilar.AnyAsync(x => x.Id != k.Id && x.KullaniciAdi.ToLower() == ad.ToLowerInvariant()))
                        return BadRequest(new { message = "Bu kullanıcı adı zaten kullanılıyor." });
                    degisiklikler.Add($"kullaniciAdi: {k.KullaniciAdi} -> {ad}");
                    k.KullaniciAdi = ad;
                }
            }
            if (request.Email != null)
            {
                var email = request.Email.Trim().ToLowerInvariant();
                var hata = HesapKurallari.EmailHata(email);
                if (hata != null) return BadRequest(new { message = hata });
                if (!string.Equals(email, k.Email, StringComparison.OrdinalIgnoreCase))
                {
                    if (await _context.Kullanicilar.AnyAsync(x => x.Id != k.Id && x.Email.ToLower() == email))
                        return BadRequest(new { message = "Bu e-posta adresi ile zaten bir hesap mevcut." });
                    degisiklikler.Add($"email: {k.Email} -> {email}");
                    k.Email = email;
                }
            }

            if (degisiklikler.Count > 0)
            {
                await _context.SaveChangesAsync();
                await _denetim.KaydetAsync(User, "hesap.profil", $"Kullanıcı #{k.Id} {k.KullaniciAdi}", string.Join("; ", degisiklikler));
            }

            return Ok(new
            {
                token = _tokenService.GenerateToken(k),
                id = k.Id,
                kullaniciAdi = k.KullaniciAdi,
                email = k.Email,
                rol = k.Rol,
                yetkiler = Izinler.Cozumle(k),
                profilResmiURL = k.ProfilResmiURL
            });
        }

        [HttpPut("sifre")]
        public async Task<IActionResult> SifreDegistir([FromBody] SifreDegistirRequest request)
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });

            var sifreHata = SifreKontrol(k, request.MevcutSifre);
            if (sifreHata != null) return sifreHata;

            var hata = HesapKurallari.SifreHata(request.YeniSifre, k.KullaniciAdi);
            if (hata != null) return BadRequest(new { message = hata });

            k.SifreHash = BCrypt.Net.BCrypt.HashPassword(request.YeniSifre!);
            k.TokenSurumu++; // diğer tüm oturumlar kapanır
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "hesap.sifre", $"Kullanıcı #{k.Id} {k.KullaniciAdi}", "Şifre değiştirildi; diğer oturumlar kapatıldı.");

            return Ok(new { token = _tokenService.GenerateToken(k) });
        }

        [HttpPost("avatar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<IActionResult> AvatarYukle([FromForm] AvatarFormu? form)
        {
            var dosya = form?.Dosya;
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });
            if (!_hizSinir.Dene($"avatar:{k.Id}", 5, TimeSpan.FromMinutes(1)))
                return StatusCode(429, new { message = "Çok sık profil resmi değiştiriyorsunuz, lütfen 1 dakika bekleyin." });

            if (dosya == null || dosya.Length == 0)
                return BadRequest(new { message = "Yüklenecek dosya seçilmedi (alan adı: dosya)." });
            if (dosya.Length > AvatarYardimcisi.MaksBoyut)
                return BadRequest(new { message = "Profil resmi en fazla 2 MB olabilir." });

            var ext = Path.GetExtension(dosya.FileName ?? string.Empty).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                return BadRequest(new { message = "Yalnızca .jpg, .png ve .webp dosyaları yüklenebilir." });

            var header = new byte[12];
            var read = 0;
            await using (var s = dosya.OpenReadStream())
            {
                while (read < header.Length)
                {
                    var n = await s.ReadAsync(header.AsMemory(read, header.Length - read));
                    if (n == 0) break;
                    read += n;
                }
            }
            var tur = AvatarYardimcisi.TurBul(header, read);
            var beklenen = ext == ".jpeg" ? "jpg" : ext.TrimStart('.');
            if (tur == null || tur != beklenen)
                return BadRequest(new { message = "Dosya içeriği uzantısıyla uyuşmuyor; geçerli bir görsel değil." });

            var klasor = AvatarYardimcisi.Klasor(_env);
            Directory.CreateDirectory(klasor);
            var ad = Guid.NewGuid().ToString("N") + "." + tur;
            var yol = Path.Combine(klasor, ad);
            try
            {
                await using var fs = new FileStream(yol, FileMode.CreateNew, FileAccess.Write);
                await dosya.CopyToAsync(fs);
            }
            catch
            {
                try { System.IO.File.Delete(yol); } catch { }
                return StatusCode(500, new { message = "Dosya kaydedilirken bir hata oluştu." });
            }

            var eski = k.ProfilResmiURL;
            k.ProfilResmiURL = $"{UploadDosyaServisi.GuvenliSunucuKoku(Request)}/avatars/{ad}";
            await _context.SaveChangesAsync();
            AvatarYardimcisi.DosyayiSil(_env, eski);
            await _denetim.KaydetAsync(User, "hesap.avatar", $"Kullanıcı #{k.Id} {k.KullaniciAdi}", "Profil resmi yüklendi.");
            var kazanim = await PuanServisi.KazandirAsync(_context, k.Id, PuanServisi.ProfilResmi, "profil_resmi");

            return Ok(new { profilResmiURL = k.ProfilResmiURL, puanKazanildi = kazanim.Kazanilan });
        }

        [HttpDelete("avatar")]
        public async Task<IActionResult> AvatarSil()
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });

            var eski = k.ProfilResmiURL;
            k.ProfilResmiURL = null;
            await _context.SaveChangesAsync();
            AvatarYardimcisi.DosyayiSil(_env, eski);
            if (eski != null)
                await _denetim.KaydetAsync(User, "hesap.avatarSil", $"Kullanıcı #{k.Id} {k.KullaniciAdi}");

            return Ok(new { message = "Profil resmi kaldırıldı." });
        }

        [HttpDelete]
        public async Task<IActionResult> HesapSil([FromBody] HesapSilRequest request)
        {
            var k = await MevcutAsync();
            if (k == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });

            if (Izinler.OwnerMi(k))
                return BadRequest(new { message = "Site sahibi hesabı silinemez." });

            var sifreHata = SifreKontrol(k, request.Sifre);
            if (sifreHata != null) return sifreHata;

            var hedef = $"Kullanıcı #{k.Id} {k.KullaniciAdi}";
            await KullaniciSilici.SilAsync(_context, _env, k);
            await _denetim.KaydetAsync(User, "hesap.sil", hedef, "Kullanıcı kendi hesabını sildi.");
            return Ok(new { message = "Hesabınız ve bağlı verileriniz silindi." });
        }
    }
}
