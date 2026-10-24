using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    /// <summary>Yorum ortak kuralları (bölüm ve seri yorumları aynı kuralları kullanır).</summary>
    public static class YorumKurallari
    {
        public const int MaksHamUzunluk = 500;
        public const int SpamSaniye = 20;

        /// <summary>Yalnızca HTML özel karakterlerini kodlar; Türkçe harfler olduğu gibi kalır.</summary>
        public static string Kodla(string metin)
            => metin.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&#39;");

        /// <summary>Kullanıcı son SpamSaniye saniye içinde (bölüm ya da seri) yorum attıysa true.</summary>
        public static async Task<bool> SpamMiAsync(AppDbContext db, int kullaniciId, CancellationToken ct)
        {
            var sinir = DateTime.UtcNow.AddSeconds(-SpamSaniye);
            if (await db.Yorumlar.AsNoTracking().AnyAsync(y => y.KullaniciId == kullaniciId && y.Tarih > sinir, ct)) return true;
            return await db.MangaYorumlari.AsNoTracking().AnyAsync(y => y.KullaniciId == kullaniciId && y.Tarih > sinir, ct);
        }

        public const string SpamMesaji = "Çok hızlı yorum yapıyorsunuz. Lütfen yaklaşık 20 saniye bekleyip tekrar deneyin.";
    }

    [ApiController]
    [Route("api")]
    public class MangaEtkilesimController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ISiteAyarServisi _ayarlar;
        private readonly YorumServisi _yorumlar;
        private readonly IslemSinirlayici _sinir;

        public MangaEtkilesimController(AppDbContext context, ISiteAyarServisi ayarlar, YorumServisi yorumlar, IslemSinirlayici sinir)
        {
            _yorumlar = yorumlar;
            _context = context;
            _ayarlar = ayarlar;
            _sinir = sinir;
        }

        private int? KullaniciId()
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private async Task<bool> MangaGorunurMuAsync(int mangaId)
        {
            var yayinda = await _context.Mangalar.AsNoTracking()
                .Where(m => m.Id == mangaId).Select(m => (bool?)m.Yayinda).FirstOrDefaultAsync();
            if (yayinda == null) return false;
            return yayinda == true || await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context);
        }

        private async Task<bool> BolumGorunurMuAsync(int bolumId)
        {
            var yayinda = await _context.Bolumler.AsNoTracking()
                .Where(b => b.Id == bolumId).Select(b => (bool?)b.Manga.Yayinda).FirstOrDefaultAsync();
            if (yayinda == null) return false;
            return yayinda == true || await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context);
        }

        // ---------------- Seri yorumları ----------------

        /// <summary>Seri yorumları (anonim). Düz liste: kök yorumlar sayfalanır (sirala=yeni|begeni), yanıtlar kökün ardından gelir.</summary>
        [HttpGet("mangas/{id:int}/yorumlar")]
        public async Task<ActionResult<IEnumerable<YorumDto>>> YorumlariGetir(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? sirala = null)
        {
            if (page < 1) page = 1;
            if (page > 100000) page = 100000;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            if (!await MangaGorunurMuAsync(id))
                return NotFound(new { message = "Seri bulunamadı." });

            var (liste, toplam) = await _yorumlar.ListeAsync(true, id, KullaniciId(), sirala, page, pageSize);
            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(liste);
        }

        [Authorize]
        [HttpPost("mangas/{id:int}/yorumlar")]
        public async Task<ActionResult<YorumDto>> YorumEkle(int id, [FromBody] CreateYorumRequest? request)
        {
            var (hata, kodlu) = YorumServisi.MetinHazirla(request?.Metin);
            if (hata != null) return BadRequest(new { message = hata });

            if (!await _ayarlar.AcikMiAsync("yorumAcik", true))
                return StatusCode(403, new { message = "Yorumlar şu an kapalı." });

            var userId = KullaniciId();
            if (userId == null) return Unauthorized();

            if (!await MangaGorunurMuAsync(id))
                return NotFound(new { message = "Yorum yapılacak seri bulunamadı." });

            var (kokId, yanitHata) = await _yorumlar.YanitHedefiAsync(true, id, request!.YanitId);
            if (yanitHata != null) return BadRequest(new { message = yanitHata });

            if (await YorumKurallari.SpamMiAsync(_context, userId.Value, HttpContext.RequestAborted))
                return StatusCode(429, new { message = YorumKurallari.SpamMesaji });

            var yorum = new MangaYorumu
            {
                MangaId = id,
                KullaniciId = userId.Value,
                Metin = kodlu,
                Tarih = DateTime.UtcNow,
                Spoiler = request.Spoiler == true,
                YanitId = kokId
            };
            _context.MangaYorumlari.Add(yorum);
            await _context.SaveChangesAsync();
            var kazanim = await PuanServisi.YorumPuaniAsync(_context, userId.Value, true, yorum.Id, request.Metin, yorum.Metin);

            var dto = await _yorumlar.TekAsync(true, id, yorum.Id, userId.Value);
            if (dto != null) dto.PuanKazanildi = kazanim.Kazanilan;
            return Ok(dto);
        }

        // ---------------- Seri puanı ----------------

        [HttpGet("mangas/{id:int}/puan")]
        public async Task<ActionResult<PuanOzetiDto>> MangaPuanGetir(int id)
        {
            if (!await MangaGorunurMuAsync(id)) return NotFound(new { message = "Seri bulunamadı." });
            return Ok(await MangaOzetAsync(id, KullaniciId()));
        }

        [Authorize]
        [HttpPut("mangas/{id:int}/puan")]
        public async Task<ActionResult<PuanOzetiDto>> MangaPuanVer(int id, [FromBody] PuanVerRequest? request)
        {
            if (request?.Deger == null || request.Deger < 1 || request.Deger > 5)
                return BadRequest(new { message = "Puan 1 ile 5 arasında bir tam sayı olmalıdır." });
            var userId = KullaniciId();
            if (userId == null) return Unauthorized();
            if (!_sinir.Dene($"puan:{userId.Value}", 30, TimeSpan.FromMinutes(1)))
                return StatusCode(429, new { message = "Çok sık puanlama yapıyorsunuz, lütfen biraz bekleyin." });
            if (!await MangaGorunurMuAsync(id)) return NotFound(new { message = "Seri bulunamadı." });

            var deger = (byte)request.Deger.Value;
            for (var deneme = 0; deneme < 2; deneme++)
            {
                var kayit = await _context.MangaPuanlari.FirstOrDefaultAsync(p => p.MangaId == id && p.KullaniciId == userId.Value);
                if (kayit == null)
                    _context.MangaPuanlari.Add(new MangaPuani { MangaId = id, KullaniciId = userId.Value, Deger = deger, Tarih = DateTime.UtcNow });
                else { kayit.Deger = deger; kayit.Tarih = DateTime.UtcNow; }
                try { await _context.SaveChangesAsync(); break; }
                catch (DbUpdateException) when (deneme == 0) { _context.ChangeTracker.Clear(); } // eşzamanlı ilk oy yarışı
            }
            var kazanim = await PuanServisi.KazandirAsync(_context, userId.Value, PuanServisi.PuanVer, "mangapuan:" + id);
            var ozet = await MangaOzetAsync(id, userId);
            ozet.PuanKazanildi = kazanim.Kazanilan;
            return Ok(ozet);
        }

        [Authorize]
        [HttpDelete("mangas/{id:int}/puan")]
        public async Task<ActionResult<PuanOzetiDto>> MangaPuanSil(int id)
        {
            var userId = KullaniciId();
            if (userId == null) return Unauthorized();
            if (!_sinir.Dene($"puan:{userId.Value}", 30, TimeSpan.FromMinutes(1)))
                return StatusCode(429, new { message = "Çok sık puanlama yapıyorsunuz, lütfen biraz bekleyin." });
            if (!await MangaGorunurMuAsync(id)) return NotFound(new { message = "Seri bulunamadı." });
            await _context.MangaPuanlari.Where(p => p.MangaId == id && p.KullaniciId == userId.Value).ExecuteDeleteAsync();
            return Ok(await MangaOzetAsync(id, userId));
        }

        // ---------------- Bölüm puanı ----------------

        [HttpGet("chapters/{id:int}/puan")]
        public async Task<ActionResult<PuanOzetiDto>> BolumPuanGetir(int id)
        {
            if (!await BolumGorunurMuAsync(id)) return NotFound(new { message = "Bölüm bulunamadı." });
            return Ok(await BolumOzetAsync(id, KullaniciId()));
        }

        [Authorize]
        [HttpPut("chapters/{id:int}/puan")]
        public async Task<ActionResult<PuanOzetiDto>> BolumPuanVer(int id, [FromBody] PuanVerRequest? request)
        {
            if (request?.Deger == null || request.Deger < 1 || request.Deger > 5)
                return BadRequest(new { message = "Puan 1 ile 5 arasında bir tam sayı olmalıdır." });
            var userId = KullaniciId();
            if (userId == null) return Unauthorized();
            if (!_sinir.Dene($"puan:{userId.Value}", 30, TimeSpan.FromMinutes(1)))
                return StatusCode(429, new { message = "Çok sık puanlama yapıyorsunuz, lütfen biraz bekleyin." });
            if (!await BolumGorunurMuAsync(id)) return NotFound(new { message = "Bölüm bulunamadı." });

            var deger = (byte)request.Deger.Value;
            for (var deneme = 0; deneme < 2; deneme++)
            {
                var kayit = await _context.BolumPuanlari.FirstOrDefaultAsync(p => p.BolumId == id && p.KullaniciId == userId.Value);
                if (kayit == null)
                    _context.BolumPuanlari.Add(new BolumPuani { BolumId = id, KullaniciId = userId.Value, Deger = deger, Tarih = DateTime.UtcNow });
                else { kayit.Deger = deger; kayit.Tarih = DateTime.UtcNow; }
                try { await _context.SaveChangesAsync(); break; }
                catch (DbUpdateException) when (deneme == 0) { _context.ChangeTracker.Clear(); }
            }
            var kazanim = await PuanServisi.KazandirAsync(_context, userId.Value, PuanServisi.PuanVer, "bolumpuan:" + id);
            var ozet = await BolumOzetAsync(id, userId);
            ozet.PuanKazanildi = kazanim.Kazanilan;
            return Ok(ozet);
        }

        [Authorize]
        [HttpDelete("chapters/{id:int}/puan")]
        public async Task<ActionResult<PuanOzetiDto>> BolumPuanSil(int id)
        {
            var userId = KullaniciId();
            if (userId == null) return Unauthorized();
            if (!_sinir.Dene($"puan:{userId.Value}", 30, TimeSpan.FromMinutes(1)))
                return StatusCode(429, new { message = "Çok sık puanlama yapıyorsunuz, lütfen biraz bekleyin." });
            if (!await BolumGorunurMuAsync(id)) return NotFound(new { message = "Bölüm bulunamadı." });
            await _context.BolumPuanlari.Where(p => p.BolumId == id && p.KullaniciId == userId.Value).ExecuteDeleteAsync();
            return Ok(await BolumOzetAsync(id, userId));
        }

        // ---------------- Özet yardımcıları ----------------

        private async Task<PuanOzetiDto> MangaOzetAsync(int id, int? userId)
        {
            var oylar = _context.MangaPuanlari.AsNoTracking().Where(p => p.MangaId == id);
            var ozet = await oylar.GroupBy(p => p.MangaId)
                .Select(g => new { Sayi = g.Count(), Toplam = g.Sum(p => (int)p.Deger) }).FirstOrDefaultAsync();
            int? benim = userId == null ? null
                : await oylar.Where(p => p.KullaniciId == userId.Value).Select(p => (int?)p.Deger).FirstOrDefaultAsync();
            return Olustur(ozet?.Sayi ?? 0, ozet?.Toplam ?? 0, benim);
        }

        private async Task<PuanOzetiDto> BolumOzetAsync(int id, int? userId)
        {
            var oylar = _context.BolumPuanlari.AsNoTracking().Where(p => p.BolumId == id);
            var ozet = await oylar.GroupBy(p => p.BolumId)
                .Select(g => new { Sayi = g.Count(), Toplam = g.Sum(p => (int)p.Deger) }).FirstOrDefaultAsync();
            int? benim = userId == null ? null
                : await oylar.Where(p => p.KullaniciId == userId.Value).Select(p => (int?)p.Deger).FirstOrDefaultAsync();
            return Olustur(ozet?.Sayi ?? 0, ozet?.Toplam ?? 0, benim);
        }

        private static PuanOzetiDto Olustur(int sayi, int toplam, int? benim) => new()
        {
            OySayisi = sayi,
            Ortalama = sayi == 0 ? null : Math.Round((decimal)toplam / sayi, 1, MidpointRounding.AwayFromZero),
            BenimPuanim = benim
        };
    }
}
