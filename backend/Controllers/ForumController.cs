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
    public record ForumKategori(string Anahtar, string Ad, string Kanji, string Aciklama, bool SadeceYetkili = false);

    public class ForumKonuIstek
    {
        public string? Kategori { get; set; }
        public string? Baslik { get; set; }
        public string? Icerik { get; set; }
        public int? MangaId { get; set; }
        public decimal? BolumNo { get; set; }
        public bool? Spoiler { get; set; }
        /// <summary>Yalnızca ekip için: konu açılırken sabitlenir.</summary>
        public bool? Sabit { get; set; }
    }

    public class ForumYanitIstek
    {
        public string? Icerik { get; set; }
        public bool? Spoiler { get; set; }
    }

    public class ForumYazarDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Resim { get; set; }
        public int Seviye { get; set; }
        public string Rutbe { get; set; } = string.Empty;
        public AdStiliDto? AdStili { get; set; }
    }

    public class ForumKonuDto
    {
        public int Id { get; set; }
        public string Kategori { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public string? Icerik { get; set; }
        public string Ozet { get; set; } = string.Empty;
        public int? MangaId { get; set; }
        public string? MangaAdi { get; set; }
        public string? MangaKapak { get; set; }
        public decimal? BolumNo { get; set; }
        public bool Spoiler { get; set; }
        /// <summary>Kullanıcı bağlı bölüme kadar okuduysa spoiler kendiliğinden açılır.</summary>
        public bool Okudu { get; set; }
        public bool Sabit { get; set; }
        public bool Kilitli { get; set; }
        public DateTime Tarih { get; set; }
        public DateTime SonYanitTarihi { get; set; }
        public int YanitSayisi { get; set; }
        public int BegeniSayisi { get; set; }
        public bool BenBegendim { get; set; }
        public bool Benim { get; set; }
        public ForumYazarDto Yazar { get; set; } = new();
        [System.Text.Json.Serialization.JsonIgnore] public int KullaniciId { get; set; }
    }

    public class ForumYanitDto
    {
        public int Id { get; set; }
        public string Icerik { get; set; } = string.Empty;
        public bool Spoiler { get; set; }
        public DateTime Tarih { get; set; }
        public bool Benim { get; set; }
        public ForumYazarDto Yazar { get; set; } = new();
        [System.Text.Json.Serialization.JsonIgnore] public int KullaniciId { get; set; }
    }

    [ApiController]
    [Route("api/forum")]
    public class ForumController : ControllerBase
    {
        public static readonly ForumKategori[] Kategoriler =
        {
            new("duyuru", "Kage Duyuruları", "告", "Ekipten haberler", true),
            new("dojo", "Dojo", "道", "Genel sohbet, her şey"),
            new("tanisma", "Tanışma", "縁", "Kendini tanıt, yoldaş bul"),
            new("seri", "Seri Sohbeti", "巻", "Bir seri hakkında konuş"),
            new("bolum", "Bölüm Tartışması", "話", "Yeni bölümü didik didik et"),
            new("teori", "Teoriler", "謎", "Tahminler, spoiler serbest"),
            new("oneri", "Öneri", "薦", "Ne okusam? Şunu oku!"),
            new("geri", "Site Sesi", "声", "Hata, istek, fikir"),
        };

        private const int MaksBaslik = 120;
        private const int MaksIcerik = 2000;

        private readonly AppDbContext _context;
        private readonly ISiteAyarServisi _ayarlar;
        private readonly IslemSinirlayici _sinir;
        private readonly TercihServisi _tercihler;
        private readonly IDenetimServisi _denetim;

        public ForumController(AppDbContext context, ISiteAyarServisi ayarlar, IslemSinirlayici sinir, TercihServisi tercihler, IDenetimServisi denetim)
        {
            _context = context;
            _ayarlar = ayarlar;
            _sinir = sinir;
            _tercihler = tercihler;
            _denetim = denetim;
        }

        private int? KullaniciId()
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private async Task<bool> YetkiliMiAsync(int uid)
        {
            var k = await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
            return k != null && Izinler.Var(k, "yorum.sil");
        }

        /// <summary>Forum metni: yorumla aynı biçim kuralları, daha uzun sınır.</summary>
        private static (string? Hata, string Kodlu) MetinHazirla(string? ham, int maks)
        {
            if (string.IsNullOrWhiteSpace(ham)) return ("Metin boş bırakılamaz.", "");
            var t = ham.Trim();
            if (t.Length > maks) return ($"Metin en fazla {maks} karakter olabilir.", "");
            var h = YorumBicimi.Dogrula(t);
            if (h != null) return (h, "");
            var kodlu = YorumKurallari.Kodla(t);
            if (kodlu.Length > 4000) return ("Metin çok fazla özel karakter içeriyor, lütfen kısaltın.", "");
            return (null, kodlu);
        }

        private async Task<bool> SpamMiAsync(int uid)
        {
            var sinir = DateTime.UtcNow.AddSeconds(-YorumKurallari.SpamSaniye);
            return await _context.ForumKonulari.AsNoTracking().AnyAsync(k => k.KullaniciId == uid && k.Tarih > sinir)
                || await _context.ForumYanitlari.AsNoTracking().AnyAsync(y => y.KullaniciId == uid && y.Tarih > sinir);
        }

        private async Task<Dictionary<int, ForumYazarDto>> YazarlarAsync(IEnumerable<int> idler)
        {
            var liste = idler.Distinct().ToList();
            if (liste.Count == 0) return new();
            var kullanicilar = await _context.Kullanicilar.AsNoTracking().Where(k => liste.Contains(k.Id))
                .Select(k => new { k.Id, k.KullaniciAdi, k.ProfilResmiURL }).ToListAsync();
            var puanlar = await _context.PuanKayitlari.AsNoTracking().Where(p => liste.Contains(p.KullaniciId) && p.Puan != 0)
                .GroupBy(p => p.KullaniciId).Select(g => new { Id = g.Key, Toplam = g.Sum(p => p.Puan) })
                .ToDictionaryAsync(x => x.Id, x => x.Toplam);
            var stiller = await _tercihler.CozAsync(liste);
            return kullanicilar.ToDictionary(k => k.Id, k =>
            {
                var seviye = PuanServisi.Seviye(puanlar.GetValueOrDefault(k.Id));
                return new ForumYazarDto
                {
                    Id = k.Id, Ad = k.KullaniciAdi, Resim = k.ProfilResmiURL, Seviye = seviye,
                    Rutbe = PuanServisi.RutbeSeviyeden(seviye), AdStili = stiller.GetValueOrDefault(k.Id)
                };
            });
        }

        private IQueryable<ForumKonuDto> Proje(IQueryable<ForumKonu> q, int uid) => q.Select(k => new ForumKonuDto
        {
            Id = k.Id, Kategori = k.Kategori, Baslik = k.Baslik,
            Ozet = k.Icerik.Length > 220 ? k.Icerik.Substring(0, 220) : k.Icerik,
            MangaId = k.MangaId,
            MangaAdi = _context.Mangalar.Where(m => m.Id == k.MangaId).Select(m => m.MangaAdi).FirstOrDefault(),
            MangaKapak = _context.Mangalar.Where(m => m.Id == k.MangaId).Select(m => m.KapakResmiURL).FirstOrDefault(),
            BolumNo = k.BolumNo, Spoiler = k.Spoiler, Sabit = k.Sabit, Kilitli = k.Kilitli,
            Okudu = uid > 0 && k.MangaId != null && k.BolumNo != null && _context.OkumaGecmisi
                .Any(o => o.KullaniciId == uid && o.MangaId == k.MangaId
                    && _context.Bolumler.Any(b => b.Id == o.SonOkunanBolumId && b.BolumNo >= k.BolumNo)),
            Tarih = k.Tarih, SonYanitTarihi = k.SonYanitTarihi, YanitSayisi = k.YanitSayisi, BegeniSayisi = k.BegeniSayisi,
            BenBegendim = uid > 0 && _context.ForumBegenileri.Any(b => b.KonuId == k.Id && b.KullaniciId == uid),
            Benim = k.KullaniciId == uid, KullaniciId = k.KullaniciId
        });

        [HttpGet("kategoriler")]
        public async Task<IActionResult> KategorileriGetir()
        {
            var sayilar = await _context.ForumKonulari.AsNoTracking().Where(k => !k.Silindi)
                .GroupBy(k => k.Kategori).Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Sayi);
            return Ok(Kategoriler.Select(k => new
            {
                k.Anahtar, k.Ad, k.Kanji, k.Aciklama, k.SadeceYetkili, Sayi = sayilar.GetValueOrDefault(k.Anahtar)
            }));
        }

        /// <summary>Konu listesi. sirala: son (son yanıt) | yeni | populer. Sabit konular ilk sayfada ayrıca döner.</summary>
        [HttpGet("konular")]
        public async Task<IActionResult> KonulariGetir([FromQuery] string? kategori, [FromQuery] int? mangaId, [FromQuery] string? sirala, [FromQuery] string? ara, [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;
            if (page > 10000) page = 10000;
            const int boyut = 20;
            var uid = KullaniciId() ?? 0;

            var q = _context.ForumKonulari.AsNoTracking().Where(k => !k.Silindi);
            if (!string.IsNullOrWhiteSpace(kategori)) q = q.Where(k => k.Kategori == kategori);
            if (mangaId != null) q = q.Where(k => k.MangaId == mangaId);
            if (!string.IsNullOrWhiteSpace(ara))
            {
                var a = ara.Trim();
                if (a.Length > 50) a = a[..50];
                q = q.Where(k => k.Baslik.Contains(a));
            }

            var toplam = await q.CountAsync(k => !k.Sabit);
            var normal = q.Where(k => !k.Sabit);
            normal = (sirala ?? "son") switch
            {
                "yeni" => normal.OrderByDescending(k => k.Tarih),
                "populer" => normal.OrderByDescending(k => k.BegeniSayisi * 3 + k.YanitSayisi).ThenByDescending(k => k.SonYanitTarihi),
                _ => normal.OrderByDescending(k => k.SonYanitTarihi)
            };
            var konular = await Proje(normal.Skip((page - 1) * boyut).Take(boyut), uid).ToListAsync();
            var sabitler = page == 1 && mangaId == null
                ? await Proje(q.Where(k => k.Sabit).OrderByDescending(k => k.Tarih).Take(5), uid).ToListAsync()
                : new List<ForumKonuDto>();

            var yazarlar = await YazarlarAsync(konular.Concat(sabitler).Select(k => k.KullaniciId));
            foreach (var k in konular.Concat(sabitler)) k.Yazar = yazarlar.GetValueOrDefault(k.KullaniciId) ?? new ForumYazarDto { Ad = "Silinmiş" };

            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(new { sabitler, konular, toplam });
        }

        [HttpGet("konular/{id:int}")]
        public async Task<IActionResult> KonuGetir(int id, [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;
            const int boyut = 30;
            var uid = KullaniciId() ?? 0;
            var konu = await Proje(_context.ForumKonulari.AsNoTracking().Where(k => k.Id == id && !k.Silindi), uid).FirstOrDefaultAsync();
            if (konu == null) return NotFound(new { message = "Konu bulunamadı." });
            konu.Icerik = await _context.ForumKonulari.AsNoTracking().Where(k => k.Id == id).Select(k => k.Icerik).FirstAsync();

            var yq = _context.ForumYanitlari.AsNoTracking().Where(y => y.KonuId == id && !y.Silindi);
            var toplam = await yq.CountAsync();
            var yanitlar = await yq.OrderBy(y => y.Tarih).ThenBy(y => y.Id).Skip((page - 1) * boyut).Take(boyut)
                .Select(y => new ForumYanitDto { Id = y.Id, Icerik = y.Icerik, Spoiler = y.Spoiler, Tarih = y.Tarih, Benim = y.KullaniciId == uid, KullaniciId = y.KullaniciId })
                .ToListAsync();

            var yazarlar = await YazarlarAsync(yanitlar.Select(y => y.KullaniciId).Append(konu.KullaniciId));
            konu.Yazar = yazarlar.GetValueOrDefault(konu.KullaniciId) ?? new ForumYazarDto { Ad = "Silinmiş" };
            foreach (var y in yanitlar) y.Yazar = yazarlar.GetValueOrDefault(y.KullaniciId) ?? new ForumYazarDto { Ad = "Silinmiş" };

            var yetkili = uid > 0 && await YetkiliMiAsync(uid);
            return Ok(new { konu, yanitlar, toplam, sayfaBoyutu = boyut, yetkili });
        }

        [Authorize]
        [HttpPost("konular")]
        public async Task<IActionResult> KonuAc([FromBody] ForumKonuIstek? istek)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();
            if (!await _ayarlar.AcikMiAsync("yorumAcik", true))
                return StatusCode(403, new { message = "Forum şu an yazmaya kapalı." });

            var kat = Kategoriler.FirstOrDefault(k => k.Anahtar == istek?.Kategori);
            if (kat == null) return BadRequest(new { message = "Geçerli bir kategori seç." });
            if (kat.SadeceYetkili && !await YetkiliMiAsync(uid.Value))
                return StatusCode(403, new { message = "Bu kategoriye yalnızca ekip yazabilir." });

            var baslik = (istek!.Baslik ?? "").Trim();
            if (baslik.Length < 4) return BadRequest(new { message = "Başlık en az 4 karakter olmalı." });
            if (baslik.Length > MaksBaslik) return BadRequest(new { message = $"Başlık en fazla {MaksBaslik} karakter olabilir." });
            var (hata, kodlu) = MetinHazirla(istek.Icerik, MaksIcerik);
            if (hata != null) return BadRequest(new { message = hata });

            int? mangaId = null; decimal? bolumNo = null;
            if (istek.MangaId != null)
            {
                var yayinda = await _context.Mangalar.AsNoTracking().Where(m => m.Id == istek.MangaId)
                    .Select(m => (bool?)m.Yayinda).FirstOrDefaultAsync();
                if (yayinda != true) return BadRequest(new { message = "Bağlanacak seri bulunamadı." });
                mangaId = istek.MangaId;
                if (istek.BolumNo != null && istek.BolumNo >= 0 && istek.BolumNo < 10000) bolumNo = istek.BolumNo;
            }

            if (!_sinir.Dene("forumkonu:" + uid, 6, TimeSpan.FromHours(1)))
                return StatusCode(429, new { message = "Bir saatte en fazla 6 konu açabilirsin." });
            if (await SpamMiAsync(uid.Value))
                return StatusCode(429, new { message = YorumKurallari.SpamMesaji });

            var konu = new ForumKonu
            {
                Kategori = kat.Anahtar, KullaniciId = uid.Value, Baslik = YorumKurallari.Kodla(baslik), Icerik = kodlu,
                MangaId = mangaId, BolumNo = bolumNo, Spoiler = istek.Spoiler == true || kat.Anahtar == "teori",
                Sabit = istek.Sabit == true && await YetkiliMiAsync(uid.Value),
                Tarih = DateTime.UtcNow, SonYanitTarihi = DateTime.UtcNow
            };
            _context.ForumKonulari.Add(konu);
            await _context.SaveChangesAsync();
            return Ok(new { id = konu.Id });
        }

        [Authorize]
        [HttpPost("konular/{id:int}/yanitlar")]
        public async Task<IActionResult> YanitYaz(int id, [FromBody] ForumYanitIstek? istek)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();
            if (!await _ayarlar.AcikMiAsync("yorumAcik", true))
                return StatusCode(403, new { message = "Forum şu an yazmaya kapalı." });
            var (hata, kodlu) = MetinHazirla(istek?.Icerik, MaksIcerik);
            if (hata != null) return BadRequest(new { message = hata });

            var konu = await _context.ForumKonulari.FirstOrDefaultAsync(k => k.Id == id && !k.Silindi);
            if (konu == null) return NotFound(new { message = "Konu bulunamadı." });
            if (konu.Kilitli && !await YetkiliMiAsync(uid.Value))
                return StatusCode(403, new { message = "Bu konu kilitli." });

            if (!_sinir.Dene("forumyanit:" + uid, 40, TimeSpan.FromHours(1)))
                return StatusCode(429, new { message = "Çok fazla yanıt yazdın, biraz dinlen." });
            if (await SpamMiAsync(uid.Value))
                return StatusCode(429, new { message = YorumKurallari.SpamMesaji });

            var yanit = new ForumYanit { KonuId = id, KullaniciId = uid.Value, Icerik = kodlu, Spoiler = istek!.Spoiler == true, Tarih = DateTime.UtcNow };
            _context.ForumYanitlari.Add(yanit);
            konu.YanitSayisi++;
            konu.SonYanitTarihi = yanit.Tarih;
            await _context.SaveChangesAsync();

            if (konu.KullaniciId != uid.Value)
            {
                var ad = User.Identity?.Name ?? "Bir kullanıcı";
                await BildirimController.BildirimKaydetAsync(_context, new[] { konu.KullaniciId }, "yanit",
                    $"{ad} forum konuna yanıt verdi", $"#/forum/konu/{id}");
            }
            return Ok(new { id = yanit.Id });
        }

        [Authorize]
        [HttpPost("konular/{id:int}/begeni")]
        public async Task<IActionResult> Begen(int id)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();
            if (!_sinir.Dene("forumbegeni:" + uid, 60, TimeSpan.FromMinutes(10)))
                return StatusCode(429, new { message = "Biraz yavaş." });
            var konu = await _context.ForumKonulari.FirstOrDefaultAsync(k => k.Id == id && !k.Silindi);
            if (konu == null) return NotFound(new { message = "Konu bulunamadı." });

            var mevcut = await _context.ForumBegenileri.FirstOrDefaultAsync(b => b.KonuId == id && b.KullaniciId == uid.Value);
            if (mevcut != null) { _context.ForumBegenileri.Remove(mevcut); konu.BegeniSayisi = Math.Max(0, konu.BegeniSayisi - 1); }
            else { _context.ForumBegenileri.Add(new ForumBegeni { KonuId = id, KullaniciId = uid.Value }); konu.BegeniSayisi++; }
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException) { return Conflict(new { message = "Tekrar dene." }); }
            return Ok(new { begendim = mevcut == null, begeniSayisi = konu.BegeniSayisi });
        }

        [Authorize]
        [HttpDelete("konular/{id:int}")]
        public async Task<IActionResult> KonuSil(int id)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();
            var konu = await _context.ForumKonulari.FirstOrDefaultAsync(k => k.Id == id && !k.Silindi);
            if (konu == null) return NotFound(new { message = "Konu bulunamadı." });
            var yetkili = konu.KullaniciId != uid.Value;
            if (yetkili && !await YetkiliMiAsync(uid.Value)) return Forbid();
            konu.Silindi = true;
            await _context.SaveChangesAsync();
            if (yetkili) await _denetim.KaydetAsync(User, "forum.konu.sil", $"konu:{id}", konu.Baslik);
            return Ok(new { message = "Konu silindi." });
        }

        [Authorize]
        [HttpDelete("yanitlar/{id:int}")]
        public async Task<IActionResult> YanitSil(int id)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();
            var yanit = await _context.ForumYanitlari.FirstOrDefaultAsync(y => y.Id == id && !y.Silindi);
            if (yanit == null) return NotFound(new { message = "Yanıt bulunamadı." });
            var yetkili = yanit.KullaniciId != uid.Value;
            if (yetkili && !await YetkiliMiAsync(uid.Value)) return Forbid();
            yanit.Silindi = true;
            var konu = await _context.ForumKonulari.FirstOrDefaultAsync(k => k.Id == yanit.KonuId);
            if (konu != null) konu.YanitSayisi = Math.Max(0, konu.YanitSayisi - 1);
            await _context.SaveChangesAsync();
            if (yetkili) await _denetim.KaydetAsync(User, "forum.yanit.sil", $"yanit:{id}", $"konu:{yanit.KonuId}");
            return Ok(new { message = "Yanıt silindi." });
        }

        /// <summary>Yetkili: sabitle / kilitle (alan=sabit|kilit, değeri tersine çevirir).</summary>
        [Yetki("yorum.sil")]
        [HttpPut("konular/{id:int}/{alan}")]
        public async Task<IActionResult> Degistir(int id, string alan)
        {
            if (alan != "sabit" && alan != "kilit") return NotFound();
            var konu = await _context.ForumKonulari.FirstOrDefaultAsync(k => k.Id == id && !k.Silindi);
            if (konu == null) return NotFound(new { message = "Konu bulunamadı." });
            if (alan == "sabit") konu.Sabit = !konu.Sabit; else konu.Kilitli = !konu.Kilitli;
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "forum." + alan, $"konu:{id}", alan == "sabit" ? konu.Sabit.ToString() : konu.Kilitli.ToString());
            return Ok(new { sabit = konu.Sabit, kilitli = konu.Kilitli });
        }
    }
}
