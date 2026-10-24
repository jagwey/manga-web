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
    [ApiController]
    [Route("api/admin")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDenetimServisi _denetim;
        private readonly ISiteAyarServisi _ayarlar;
        private readonly YorumServisi _yorumServisi;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext context, IDenetimServisi denetim, ISiteAyarServisi ayarlar, IWebHostEnvironment env, YorumServisi yorumServisi)
        {
            _yorumServisi = yorumServisi;
            _context = context;
            _denetim = denetim;
            _ayarlar = ayarlar;
            _env = env;
        }

        // ---- Yardımcılar ----

        private async Task<Kullanici?> MevcutKullaniciAsync()
        {
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idStr, out var id)) return null;
            return await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id);
        }

        private ObjectResult Yasak(string mesaj = "Bu işlem için yetkiniz yok.")
            => StatusCode(StatusCodes.Status403Forbidden, new { message = mesaj });

        /// <summary>Giriş yapmış, aktif kullanıcıyı döner; değilse uygun hata sonucunu verir.</summary>
        private async Task<(Kullanici? Kullanici, IActionResult? Hata)> GirisliAsync()
        {
            var k = await MevcutKullaniciAsync();
            if (k == null) return (null, Unauthorized(new { message = "Oturumunuz geçersiz." }));
            if (await HesapDurumu.BanliMiAsync(_context, k)) return (null, Yasak("Hesabınız kapatılmış."));
            return (k, null);
        }

        private async Task<(Kullanici? Kullanici, IActionResult? Hata)> PanelAsync()
        {
            var (k, hata) = await GirisliAsync();
            if (hata != null) return (null, hata);
            if (!Izinler.PaneleErisir(k!)) return (null, Yasak());
            return (k, null);
        }

        private async Task<(Kullanici? Kullanici, IActionResult? Hata)> OwnerAsync()
        {
            var (k, hata) = await GirisliAsync();
            if (hata != null) return (null, hata);
            if (!Izinler.OwnerMi(k!)) return (null, Yasak("Bu işlemi yalnızca site sahibi yapabilir."));
            return (k, null);
        }

        private static YoneticiDto YoneticiDtoOlustur(Kullanici k) => new()
        {
            Id = k.Id,
            KullaniciAdi = k.KullaniciAdi,
            Email = k.Email,
            Rol = k.Rol,
            Yetkiler = Izinler.Cozumle(k),
            Aktif = k.Aktif,
            KayitTarihi = k.KayitTarihi
        };

        private static (List<string>? Liste, string? Hata) YetkileriDogrula(List<string>? istenen)
        {
            var liste = new List<string>();
            foreach (var a in istenen ?? new List<string>())
            {
                var anahtar = (a ?? string.Empty).Trim();
                if (!Izinler.Gecerli(anahtar)) return (null, $"Bilinmeyen izin anahtarı: {anahtar}");
                if (!liste.Contains(anahtar)) liste.Add(anahtar);
            }
            return (liste, null);
        }

        private static (int Page, int PageSize) Sayfalama(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (page > 100000) page = 100000;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;
            return (page, pageSize);
        }

        // ---- Herkes (giriş yapmış) ----

        [HttpGet("ben")]
        public async Task<IActionResult> Ben()
        {
            var (k, hata) = await GirisliAsync();
            if (hata != null) return hata;
            return Ok(new
            {
                id = k!.Id,
                kullaniciAdi = k.KullaniciAdi,
                rol = k.Rol,
                yetkiler = Izinler.Cozumle(k)
            });
        }

        // ---- Panele erişimi olanlar ----

        [HttpGet("yetkiler")]
        public async Task<IActionResult> YetkiKatalogu()
        {
            var (_, hata) = await PanelAsync();
            if (hata != null) return hata;
            return Ok(Izinler.Katalog.Select(i => new { anahtar = i.Anahtar, ad = i.Ad, aciklama = i.Aciklama }));
        }

        [HttpGet("ozet")]
        public async Task<IActionResult> Ozet()
        {
            var (_, hata) = await PanelAsync();
            if (hata != null) return hata;

            var sonMangalar = await _context.Mangalar.AsNoTracking()
                .OrderByDescending(m => m.EklenmeTarihi).ThenByDescending(m => m.Id)
                .Take(5)
                .Select(m => new { id = m.Id, mangaAdi = m.MangaAdi, eklenmeTarihi = m.EklenmeTarihi })
                .ToListAsync();

            return Ok(new
            {
                mangaSayisi = await _context.Mangalar.CountAsync(),
                bolumSayisi = await _context.Bolumler.CountAsync(),
                sayfaSayisi = await _context.Sayfalar.CountAsync(),
                kullaniciSayisi = await _context.Kullanicilar.CountAsync(),
                yorumSayisi = await _context.Yorumlar.CountAsync(),
                mangaYorumSayisi = await _context.MangaYorumlari.CountAsync(),
                sonMangalar
            });
        }

        // ---- Yalnızca Owner: yönetici yönetimi ----

        [HttpGet("yoneticiler")]
        public async Task<IActionResult> Yoneticiler()
        {
            var (_, hata) = await OwnerAsync();
            if (hata != null) return hata;

            var liste = await _context.Kullanicilar.AsNoTracking()
                .Where(k => k.Rol == Roller.Owner || k.Rol == Roller.Admin)
                .OrderBy(k => k.Rol == Roller.Owner ? 0 : 1).ThenBy(k => k.KullaniciAdi)
                .ToListAsync();
            return Ok(liste.Select(YoneticiDtoOlustur));
        }

        [HttpPost("yoneticiler")]
        public async Task<IActionResult> YoneticiEkle([FromBody] YoneticiEkleRequest request)
        {
            var (_, hata) = await OwnerAsync();
            if (hata != null) return hata;

            var sorgu = (request?.KullaniciAdiOrEmail ?? string.Empty).Trim().ToLowerInvariant();
            if (sorgu.Length == 0)
                return BadRequest(new { message = "Kullanıcı adı veya e-posta girilmelidir." });

            var (yetkiler, yetkiHata) = YetkileriDogrula(request!.Yetkiler);
            if (yetkiHata != null) return BadRequest(new { message = yetkiHata });

            var hedef = await _context.Kullanicilar
                .FirstOrDefaultAsync(k => k.KullaniciAdi.ToLower() == sorgu || k.Email.ToLower() == sorgu);
            if (hedef == null)
                return NotFound(new { message = "Bu kullanıcı adı veya e-posta ile kayıtlı kullanıcı bulunamadı." });
            if (Izinler.OwnerMi(hedef) || Izinler.AdminMi(hedef))
                return BadRequest(new { message = "Bu kullanıcı zaten yönetici veya site sahibi." });

            if (!hedef.Aktif)
                return BadRequest(new { message = "Banlı kullanıcı yönetici yapılamaz." });

            hedef.Rol = Roller.Admin;
            hedef.Yetkiler = Izinler.Birlestir(yetkiler!);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "yonetici.ekle", $"Kullanıcı #{hedef.Id} {hedef.KullaniciAdi}", "İzinler: " + (hedef.Yetkiler ?? "yok"));
            return Ok(YoneticiDtoOlustur(hedef));
        }

        [HttpPut("yoneticiler/{id:int}")]
        public async Task<IActionResult> YoneticiGuncelle(int id, [FromBody] YoneticiGuncelleRequest request)
        {
            var (_, hata) = await OwnerAsync();
            if (hata != null) return hata;

            var hedef = await _context.Kullanicilar.FindAsync(id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            if (Izinler.OwnerMi(hedef)) return Yasak("Site sahibinin yetkileri değiştirilemez.");
            if (!Izinler.AdminMi(hedef)) return BadRequest(new { message = "Bu kullanıcı yönetici değil." });

            var (yetkiler, yetkiHata) = YetkileriDogrula(request?.Yetkiler);
            if (yetkiHata != null) return BadRequest(new { message = yetkiHata });

            hedef.Yetkiler = Izinler.Birlestir(yetkiler!);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "yonetici.izinler", $"Kullanıcı #{hedef.Id} {hedef.KullaniciAdi}", "İzinler: " + (hedef.Yetkiler ?? "yok"));
            return Ok(YoneticiDtoOlustur(hedef));
        }

        [HttpDelete("yoneticiler/{id:int}")]
        public async Task<IActionResult> YoneticiKaldir(int id)
        {
            var (_, hata) = await OwnerAsync();
            if (hata != null) return hata;

            var hedef = await _context.Kullanicilar.FindAsync(id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            if (Izinler.OwnerMi(hedef)) return Yasak("Site sahibinin yöneticiliği kaldırılamaz.");
            if (!Izinler.AdminMi(hedef)) return BadRequest(new { message = "Bu kullanıcı yönetici değil." });

            hedef.Rol = Roller.User;
            hedef.Yetkiler = null;
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "yonetici.kaldir", $"Kullanıcı #{hedef.Id} {hedef.KullaniciAdi}");
            return Ok(YoneticiDtoOlustur(hedef));
        }

        // ---- Kullanıcılar ----

        /// <summary>[Yetki] filtresinin DB'den çözdüğü çağıran kullanıcı.</summary>
        private Kullanici Cagiran => (Kullanici)HttpContext.Items["Kullanici"]!;

        private static string KullaniciHedefi(Kullanici k) => $"Kullanıcı #{k.Id} {k.KullaniciAdi}";

        /// <summary>
        /// Hedef kuralları: Owner hedeflenemez (403); kendine uygulanamayan işlemlerde (ban/silme) 400;
        /// Admin hedefine yalnızca Owner müdahale edebilir (403).
        /// </summary>
        private IActionResult? HedefEngeli(Kullanici hedef, bool kendineYasak = false, string kendiMesaji = "Bu işlemi kendi hesabınıza uygulayamazsınız.")
        {
            if (Izinler.OwnerMi(hedef)) return Yasak("Site sahibinin hesabına yönetim uçlarından müdahale edilemez.");
            if (kendineYasak && hedef.Id == Cagiran.Id) return BadRequest(new { message = kendiMesaji });
            if (Izinler.AdminMi(hedef) && !Izinler.OwnerMi(Cagiran)) return Yasak("Yönetici hesaplarına yalnızca site sahibi müdahale edebilir.");
            return null;
        }

        [Yetki("kullanici.gor")]
        [HttpGet("kullanicilar")]
        public async Task<IActionResult> Kullanicilar([FromQuery] string? search, [FromQuery] string? durum, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            (page, pageSize) = Sayfalama(page, pageSize);
            await HesapDurumu.SuresiDolanlariTemizleAsync(_context);

            var q = _context.Kullanicilar.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                q = q.Where(k => k.KullaniciAdi.ToLower().Contains(s) || k.Email.ToLower().Contains(s));
            }
            switch ((durum ?? "hepsi").Trim().ToLowerInvariant())
            {
                case "banli": q = q.Where(k => !k.Aktif); break;
                case "yonetici": q = q.Where(k => k.Rol == Roller.Owner || k.Rol == Roller.Admin); break;
                case "hepsi": break;
                default: return BadRequest(new { message = "durum değeri hepsi, banli veya yonetici olmalıdır." });
            }

            var toplam = await q.CountAsync();
            var sonuc = await q.OrderBy(k => k.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(k => new
                {
                    id = k.Id,
                    kullaniciAdi = k.KullaniciAdi,
                    email = k.Email,
                    rol = k.Rol,
                    aktif = k.Aktif,
                    kayitTarihi = k.KayitTarihi,
                    sonGirisTarihi = k.SonGirisTarihi,
                    yorumSayisi = k.Yorumlar.Count,
                    banNedeni = k.BanNedeni,
                    banBitis = k.BanBitis
                })
                .ToListAsync();

            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(sonuc);
        }

        [Yetki("kullanici.gor")]
        [HttpGet("kullanicilar/{id:int}")]
        public async Task<IActionResult> KullaniciDetay(int id)
        {
            await HesapDurumu.SuresiDolanlariTemizleAsync(_context, id);
            var k = await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (k == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

            object? ban = null;
            if (!k.Aktif)
            {
                string? yapanAdi = null;
                if (k.BanYapanId != null)
                    yapanAdi = await _context.Kullanicilar.AsNoTracking().Where(x => x.Id == k.BanYapanId)
                        .Select(x => x.KullaniciAdi).FirstOrDefaultAsync();
                ban = new { neden = k.BanNedeni, tarih = k.BanTarihi, bitis = k.BanBitis, yapanAdi };
            }

            return Ok(new
            {
                id = k.Id,
                kullaniciAdi = k.KullaniciAdi,
                email = k.Email,
                rol = k.Rol,
                yetkiler = Izinler.Cozumle(k),
                aktif = k.Aktif,
                kayitTarihi = k.KayitTarihi,
                sonGirisTarihi = k.SonGirisTarihi,
                profilResmiURL = k.ProfilResmiURL,
                yorumSayisi = await _context.Yorumlar.CountAsync(y => y.KullaniciId == id),
                mangaYorumSayisi = await _context.MangaYorumlari.CountAsync(y => y.KullaniciId == id),
                favoriSayisi = await _context.Favoriler.CountAsync(f => f.KullaniciId == id),
                okumaKaydiSayisi = await _context.OkumaGecmisi.CountAsync(o => o.KullaniciId == id),
                toplamPuan = await _context.PuanKayitlari.Where(p => p.KullaniciId == id && p.Puan > 0).SumAsync(p => (int?)p.Puan) ?? 0,
                ban
            });
        }

        [Yetki("kullanici.duzenle")]
        [HttpPut("kullanicilar/{id:int}")]
        public async Task<IActionResult> KullaniciDuzenle(int id, [FromBody] KullaniciDuzenleRequest request)
        {
            var hedef = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Id == id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var engel = HedefEngeli(hedef);
            if (engel != null) return engel;

            var degisiklikler = new List<string>();
            if (request.KullaniciAdi != null)
            {
                var ad = request.KullaniciAdi.Trim();
                var hata = HesapKurallari.KullaniciAdiHata(ad);
                if (hata != null) return BadRequest(new { message = hata });
                if (!string.Equals(ad, hedef.KullaniciAdi, StringComparison.Ordinal))
                {
                    if (await _context.Kullanicilar.AnyAsync(k => k.Id != id && k.KullaniciAdi.ToLower() == ad.ToLowerInvariant()))
                        return BadRequest(new { message = "Bu kullanıcı adı zaten kullanılıyor." });
                    degisiklikler.Add($"kullaniciAdi: {hedef.KullaniciAdi} -> {ad}");
                    hedef.KullaniciAdi = ad;
                }
            }
            if (request.Email != null)
            {
                var email = request.Email.Trim().ToLowerInvariant();
                var hata = HesapKurallari.EmailHata(email);
                if (hata != null) return BadRequest(new { message = hata });
                if (!string.Equals(email, hedef.Email, StringComparison.OrdinalIgnoreCase))
                {
                    if (await _context.Kullanicilar.AnyAsync(k => k.Id != id && k.Email.ToLower() == email))
                        return BadRequest(new { message = "Bu e-posta adresi ile zaten bir hesap mevcut." });
                    degisiklikler.Add($"email: {hedef.Email} -> {email}");
                    hedef.Email = email;
                }
            }

            if (degisiklikler.Count > 0)
            {
                await _context.SaveChangesAsync();
                await _denetim.KaydetAsync(User, "kullanici.duzenle", KullaniciHedefi(hedef), string.Join("; ", degisiklikler));
            }
            return Ok(new { id = hedef.Id, kullaniciAdi = hedef.KullaniciAdi, email = hedef.Email, rol = hedef.Rol, aktif = hedef.Aktif });
        }

        [Yetki("kullanici.sifre")]
        [HttpPost("kullanicilar/{id:int}/sifre")]
        public async Task<IActionResult> SifreSifirla(int id, [FromBody] SifreSifirlaRequest? request = null)
        {
            var hedef = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Id == id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var engel = HedefEngeli(hedef);
            if (engel != null) return engel;

            var verilen = !string.IsNullOrEmpty(request?.YeniSifre);
            string sifre;
            if (verilen)
            {
                sifre = request!.YeniSifre!;
                var hata = HesapKurallari.SifreHata(sifre, hedef.KullaniciAdi);
                if (hata != null) return BadRequest(new { message = hata });
            }
            else
            {
                sifre = HesapKurallari.GeciciSifreUret();
            }

            hedef.SifreHash = BCrypt.Net.BCrypt.HashPassword(sifre);
            hedef.TokenSurumu++;
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "kullanici.sifre", KullaniciHedefi(hedef),
                verilen ? "Şifre yönetici tarafından belirlendi; oturumlar kapatıldı." : "Geçici şifre üretildi; oturumlar kapatıldı.");

            return Ok(verilen ? new { tamam = true } : (object)new { geciciSifre = sifre });
        }

        [Yetki("kullanici.engelle")]
        [HttpPost("kullanicilar/{id:int}/ban")]
        public async Task<IActionResult> Banla(int id, [FromBody] BanRequest request)
        {
            var hedef = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Id == id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var engel = HedefEngeli(hedef, kendineYasak: true, kendiMesaji: "Kendi hesabınızı banlayamazsınız.");
            if (engel != null) return engel;

            var neden = (request.Neden ?? string.Empty).Trim();
            if (neden.Length == 0) return BadRequest(new { message = "Ban nedeni zorunludur." });
            if (neden.Length > 500) return BadRequest(new { message = "Ban nedeni en fazla 500 karakter olabilir." });

            DateTime? bitis = null;
            if (request.Bitis != null)
            {
                bitis = request.Bitis.Value.UtcDateTime;
                if (bitis <= DateTime.UtcNow) return BadRequest(new { message = "Ban bitiş tarihi gelecekte olmalıdır." });
            }

            hedef.Aktif = false;
            hedef.BanNedeni = neden;
            hedef.BanTarihi = DateTime.UtcNow;
            hedef.BanBitis = bitis;
            hedef.BanYapanId = Cagiran.Id;
            hedef.TokenSurumu++;
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "kullanici.ban", KullaniciHedefi(hedef),
                $"Neden: {neden}; Bitiş: {(bitis == null ? "süresiz" : bitis.Value.ToString("u"))}");

            return Ok(new { id = hedef.Id, kullaniciAdi = hedef.KullaniciAdi, aktif = hedef.Aktif, banNedeni = hedef.BanNedeni, banBitis = hedef.BanBitis });
        }

        [Yetki("kullanici.engelle")]
        [HttpPost("kullanicilar/{id:int}/ban-kaldir")]
        public async Task<IActionResult> BanKaldir(int id)
        {
            var hedef = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Id == id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var engel = HedefEngeli(hedef);
            if (engel != null) return engel;

            if (!HesapDurumu.BanliMi(hedef))
            {
                if (HesapDurumu.BanSuresiDoldu(hedef)) { HesapDurumu.BaniTemizle(hedef); await _context.SaveChangesAsync(); }
                return BadRequest(new { message = "Kullanıcı banlı değil." });
            }

            HesapDurumu.BaniTemizle(hedef);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "kullanici.banKaldir", KullaniciHedefi(hedef));
            return Ok(new { id = hedef.Id, kullaniciAdi = hedef.KullaniciAdi, aktif = hedef.Aktif });
        }

        [Yetki("kullanici.engelle")]
        [HttpPost("kullanicilar/{id:int}/oturumlari-kapat")]
        public async Task<IActionResult> OturumlariKapat(int id)
        {
            var hedef = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Id == id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var engel = HedefEngeli(hedef);
            if (engel != null) return engel;

            hedef.TokenSurumu++;
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "kullanici.oturumKapat", KullaniciHedefi(hedef));
            return Ok(new { message = "Kullanıcının tüm oturumları kapatıldı." });
        }

        [Yetki("kullanici.sil")]
        [HttpDelete("kullanicilar/{id:int}")]
        public async Task<IActionResult> KullaniciSil(int id)
        {
            var hedef = await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var engel = HedefEngeli(hedef, kendineYasak: true, kendiMesaji: "Kendi hesabınızı buradan silemezsiniz.");
            if (engel != null) return engel;

            var hedefAdi = KullaniciHedefi(hedef);
            await KullaniciSilici.SilAsync(_context, _env, hedef);
            await _denetim.KaydetAsync(User, "kullanici.sil", hedefAdi, $"E-posta: {hedef.Email}");
            return Ok(new { message = "Kullanıcı ve bağlı verileri silindi." });
        }

        /// <summary>Suistimal durumunda kullanıcının tüm puan kayıtlarını siler.</summary>
        [Yetki("kullanici.engelle")]
        [HttpDelete("kullanicilar/{id:int}/puanlar")]
        public async Task<IActionResult> KullaniciPuanlariniSil(int id)
        {
            var hedef = await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var engel = HedefEngeli(hedef);
            if (engel != null) return engel;

            var silinen = await _context.PuanKayitlari.Where(p => p.KullaniciId == id).ExecuteDeleteAsync();
            PuanServisi.Gecersizlestir();
            await _denetim.KaydetAsync(User, "kullanici.puanSifirla", KullaniciHedefi(hedef), $"{silinen} puan kaydı silindi.");
            return Ok(new { message = "Kullanıcının tüm puanları silindi.", silinenKayit = silinen });
        }

        [Yetki("kullanici.gor")]
        [HttpGet("kullanicilar/{id:int}/yorumlar")]
        public async Task<IActionResult> KullaniciYorumlari(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? tur = null)
        {
            (page, pageSize) = Sayfalama(page, pageSize);
            if (!TurGecerli(tur, out var manga)) return BadRequest(new { message = "tur 'bolum' ya da 'manga' olmalıdır." });
            if (!await _context.Kullanicilar.AnyAsync(k => k.Id == id))
                return NotFound(new { message = "Kullanıcı bulunamadı." });

            if (manga)
            {
                var qm = _context.MangaYorumlari.AsNoTracking().Where(y => y.KullaniciId == id);
                var toplamM = await qm.CountAsync();
                var sonucM = await qm.OrderByDescending(y => y.Tarih).ThenByDescending(y => y.Id)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(y => new
                    {
                        id = y.Id,
                        tur = "manga",
                        metin = y.Metin,
                        tarih = y.Tarih,
                        kullaniciAdi = y.Kullanici.KullaniciAdi,
                        mangaAdi = y.Manga.MangaAdi,
                        mangaId = y.MangaId
                    })
                    .ToListAsync();
                Response.Headers["X-Total-Count"] = toplamM.ToString();
                return Ok(sonucM);
            }

            var q = _context.Yorumlar.AsNoTracking().Where(y => y.KullaniciId == id);
            var toplam = await q.CountAsync();
            var sonuc = await q.OrderByDescending(y => y.Tarih).ThenByDescending(y => y.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(y => new
                {
                    id = y.Id,
                    tur = "bolum",
                    metin = y.Metin,
                    tarih = y.Tarih,
                    kullaniciAdi = y.Kullanici.KullaniciAdi,
                    mangaAdi = y.Bolum.Manga.MangaAdi,
                    mangaId = y.Bolum.MangaId,
                    bolumNo = y.Bolum.BolumNo,
                    bolumId = y.BolumId
                })
                .ToListAsync();

            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(sonuc);
        }

        /// <summary>Eski uç: kapatma = nedensiz süresiz ban, açma = ban kaldırma.</summary>
        [Yetki("kullanici.engelle")]
        [HttpPut("kullanicilar/{id:int}/durum")]
        public async Task<IActionResult> DurumDegistir(int id, [FromBody] DurumRequest request)
        {
            var hedef = await _context.Kullanicilar.FindAsync(id);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

            if (Izinler.OwnerMi(hedef))
                return Yasak("Site sahibinin hesap durumu değiştirilemez.");
            if (hedef.Id == Cagiran.Id)
                return BadRequest(new { message = "Kendi hesabınızı kapatamazsınız." });
            if (Izinler.AdminMi(hedef) && !Izinler.OwnerMi(Cagiran))
                return Yasak("Yönetici hesaplarının durumunu yalnızca site sahibi değiştirebilir.");

            if (request.Aktif)
            {
                HesapDurumu.BaniTemizle(hedef);
            }
            else
            {
                hedef.Aktif = false;
                hedef.BanNedeni = null;
                hedef.BanTarihi = DateTime.UtcNow;
                hedef.BanBitis = null;
                hedef.BanYapanId = Cagiran.Id;
                hedef.TokenSurumu++;
            }
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, request.Aktif ? "kullanici.banKaldir" : "kullanici.ban", KullaniciHedefi(hedef),
                request.Aktif ? null : "Nedensiz süresiz kapatma (eski uç)");
            return Ok(new { id = hedef.Id, kullaniciAdi = hedef.KullaniciAdi, rol = hedef.Rol, aktif = hedef.Aktif });
        }

        // ---- Denetim kayıtları ----

        [Yetki("kayit.gor")]
        [HttpGet("kayitlar")]
        public async Task<IActionResult> Kayitlar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? arama = null, [FromQuery] string? eylem = null)
        {
            (page, pageSize) = Sayfalama(page, pageSize);
            var q = _context.DenetimKayitlari.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(arama))
            {
                var a = arama.Trim();
                q = q.Where(d => d.YapanAdi.Contains(a) || d.Eylem.Contains(a) || d.Hedef.Contains(a));
            }
            if (!string.IsNullOrWhiteSpace(eylem))
            {
                var e = eylem.Trim();
                q = q.Where(d => d.Eylem.StartsWith(e));
            }

            var toplam = await q.CountAsync();
            var sonuc = await q.OrderByDescending(d => d.Tarih).ThenByDescending(d => d.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(d => new { id = d.Id, yapanId = d.YapanId, yapanAdi = d.YapanAdi, eylem = d.Eylem, hedef = d.Hedef, detay = d.Detay, tarih = d.Tarih })
                .ToListAsync();

            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(sonuc);
        }

        // ---- Site ayarları ----

        [Yetki("ayar.yonet")]
        [HttpGet("ayarlar")]
        public async Task<IActionResult> AyarlariGetir() => Ok(await AyarYardimcisi.OkuAsync(_ayarlar));

        [Yetki("ayar.yonet")]
        [HttpPut("ayarlar")]
        public async Task<IActionResult> AyarlariGuncelle([FromBody] AyarlarGuncelleRequest request)
        {
            if (request.BakimMesaji != null && request.BakimMesaji.Trim().Length > 300)
                return BadRequest(new { message = "Bakım mesajı en fazla 300 karakter olabilir." });
            if (request.DuyuruMetni != null && request.DuyuruMetni.Trim().Length > 500)
                return BadRequest(new { message = "Duyuru metni en fazla 500 karakter olabilir." });

            var onceki = await AyarYardimcisi.OkuAsync(_ayarlar);
            var degisen = new List<string>();

            async Task B(string anahtar, bool? yeni, bool eski)
            {
                if (yeni == null || yeni.Value == eski) return;
                await _ayarlar.KaydetAsync(anahtar, yeni.Value ? "1" : "0");
                degisen.Add($"{anahtar}: {(eski ? 1 : 0)} -> {(yeni.Value ? 1 : 0)}");
            }
            async Task S(string anahtar, string? yeni, string eski)
            {
                if (yeni == null) return;
                yeni = yeni.Trim();
                if (yeni == eski) return;
                await _ayarlar.KaydetAsync(anahtar, yeni);
                degisen.Add($"{anahtar} değişti");
            }

            await B("kayitAcik", request.KayitAcik, onceki.KayitAcik);
            await B("yorumAcik", request.YorumAcik, onceki.YorumAcik);
            await B("bakimModu", request.BakimModu, onceki.BakimModu);
            await S("bakimMesaji", request.BakimMesaji, onceki.BakimMesaji);
            await B("duyuruAcik", request.DuyuruAcik, onceki.DuyuruAcik);
            await S("duyuruMetni", request.DuyuruMetni, onceki.DuyuruMetni);
            await B("sohbetAcik", request.SohbetAcik, onceki.SohbetAcik);

            if (degisen.Count > 0)
                await _denetim.KaydetAsync(User, "ayar.guncelle", "Site ayarları", string.Join("; ", degisen));

            return Ok(await AyarYardimcisi.OkuAsync(_ayarlar));
        }

        // ---- Yorumlar ----

        private static bool TurGecerli(string? tur, out bool manga)
        {
            manga = false;
            if (string.IsNullOrWhiteSpace(tur) || tur.Equals("bolum", StringComparison.OrdinalIgnoreCase)) return true;
            if (tur.Equals("manga", StringComparison.OrdinalIgnoreCase)) { manga = true; return true; }
            return false;
        }

        [Yetki("yorum.sil")]
        [HttpGet("yorumlar")]
        public async Task<IActionResult> Yorumlar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? tur = null)
        {
            (page, pageSize) = Sayfalama(page, pageSize);
            if (!TurGecerli(tur, out var manga)) return BadRequest(new { message = "tur 'bolum' ya da 'manga' olmalıdır." });

            if (manga)
            {
                var toplamM = await _context.MangaYorumlari.CountAsync();
                var sonucM = await _context.MangaYorumlari.AsNoTracking()
                    .OrderByDescending(y => y.Tarih).ThenByDescending(y => y.Id)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(y => new
                    {
                        id = y.Id,
                        tur = "manga",
                        metin = y.Metin,
                        tarih = y.Tarih,
                        kullaniciAdi = y.Kullanici.KullaniciAdi,
                        mangaAdi = y.Manga.MangaAdi,
                        mangaId = y.MangaId
                    })
                    .ToListAsync();
                Response.Headers["X-Total-Count"] = toplamM.ToString();
                return Ok(sonucM);
            }

            var toplam = await _context.Yorumlar.CountAsync();
            var sonuc = await _context.Yorumlar.AsNoTracking()
                .OrderByDescending(y => y.Tarih).ThenByDescending(y => y.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(y => new
                {
                    id = y.Id,
                    tur = "bolum",
                    metin = y.Metin,
                    tarih = y.Tarih,
                    kullaniciAdi = y.Kullanici.KullaniciAdi,
                    mangaAdi = y.Bolum.Manga.MangaAdi,
                    mangaId = y.Bolum.MangaId,
                    bolumNo = y.Bolum.BolumNo,
                    bolumId = y.BolumId
                })
                .ToListAsync();

            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(sonuc);
        }

        [Yetki("yorum.sil")]
        [HttpDelete("yorumlar/{id:int}")]
        public async Task<IActionResult> YorumSil(int id, [FromQuery] string? tur = null)
        {
            if (!TurGecerli(tur, out var manga)) return BadRequest(new { message = "tur 'bolum' ya da 'manga' olmalıdır." });

            var bilgi = await _yorumServisi.BilgiAsync(manga, id);
            if (bilgi == null) return NotFound(new { message = "Yorum bulunamadı." });

            var hedef = manga ? $"Seri yorumu #{id} (kullanıcı #{bilgi.KullaniciId})" : $"Yorum #{id} (kullanıcı #{bilgi.KullaniciId})";
            var metin = bilgi.Metin.Length > 200 ? bilgi.Metin.Substring(0, 200) : bilgi.Metin;
            var silinen = await _yorumServisi.SilAsync(manga, id); // yanıtlar, beğeniler, şikayetler ve puan satırları da temizlenir
            await _denetim.KaydetAsync(User, "yorum.sil", hedef, silinen.Count > 1 ? metin + $" (+{silinen.Count - 1} yanıt)" : metin);
            return Ok(new { message = "Yorum silindi." });
        }
    }
}
