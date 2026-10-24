using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api/liderlik")]
    public class LiderlikController : ControllerBase
    {
        private const int MaksLimit = 100;
        private static readonly TimeSpan OnbellekSuresi = TimeSpan.FromSeconds(60);

        private sealed class Siralama
        {
            public DateTime Guncelleme { get; init; }
            public Dictionary<int, (int Sira, int Puan)> Kullanicilar { get; init; } = new();
            public List<LiderlikSatiriDto> Ust { get; init; } = new();
        }

        private readonly AppDbContext _context;
        private readonly IMemoryCache _onbellek;
        private readonly TercihServisi _tercihler;

        public LiderlikController(AppDbContext context, IMemoryCache onbellek, TercihServisi tercihler)
        {
            _tercihler = tercihler;
            _context = context;
            _onbellek = onbellek;
        }

        /// <summary>Liderlik tablosu (anonim). donem: hafta | ay | tum (varsayılan).</summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<LiderlikYanitiDto>> Getir([FromQuery] string? donem = "tum", [FromQuery] int limit = 50)
        {
            donem = (donem ?? "tum").Trim().ToLowerInvariant();
            if (donem != "hafta" && donem != "ay" && donem != "tum")
                return BadRequest(new { message = "donem 'hafta', 'ay' ya da 'tum' olmalıdır." });
            if (limit < 1) limit = 50;
            if (limit > MaksLimit) limit = MaksLimit;

            var s = await SiralamaAlAsync(donem);
            var yanit = new LiderlikYanitiDto
            {
                Donem = donem,
                Guncelleme = s.Guncelleme,
                Liste = s.Ust.Take(limit).ToList()
            };

            if (User.Identity?.IsAuthenticated == true
                && int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid))
            {
                var puan = 0;
                int? sira = null;
                if (s.Kullanicilar.TryGetValue(uid, out var k)) { sira = k.Sira; puan = k.Puan; }
                var toplam = donem == "tum" ? puan : await ToplamAsync(uid);
                var (sonraki, kalan) = PuanServisi.SonrakiRutbe(toplam);
                yanit.Ben = new LiderlikBenDto
                {
                    Sira = sira,
                    Puan = puan,
                    Rutbe = PuanServisi.Rutbe(toplam),
                    Seviye = PuanServisi.Seviye(toplam),
                    SeviyeIcinGereken = PuanServisi.SeviyeIcinGereken(PuanServisi.Seviye(toplam)),
                    SonrakiSeviyeIcinKalan = PuanServisi.SonrakiSeviyeIcinKalan(toplam),
                    SonrakiRutbe = sonraki,
                    SonrakiRutbeIcinKalan = kalan
                };
            }
            return Ok(yanit);
        }

        private async Task<int> ToplamAsync(int uid)
            => await _context.PuanKayitlari.AsNoTracking().Where(p => p.KullaniciId == uid && p.Puan > 0).SumAsync(p => (int?)p.Puan) ?? 0;

        private async Task<Siralama> SiralamaAlAsync(string donem)
        {
            var anahtar = $"liderlik:{donem}:{PuanServisi.Surum}";
            if (_onbellek.TryGetValue(anahtar, out Siralama? hazir) && hazir != null) return hazir;

            DateTime? baslangic = null;
            var yerel = PuanServisi.YerelSimdi();
            if (donem == "hafta")
            {
                var fark = ((int)yerel.DayOfWeek + 6) % 7; // Pazartesi = 0
                baslangic = PuanServisi.YerelGunBaslangicUtc(yerel.Date.AddDays(-fark));
            }
            else if (donem == "ay")
            {
                baslangic = PuanServisi.YerelGunBaslangicUtc(new DateTime(yerel.Year, yerel.Month, 1));
            }

            var simdi = DateTime.UtcNow;
            // Banlı (Aktif=false ve ban süresi dolmamış) kullanıcılar listelenmez.
            var q = _context.PuanKayitlari.AsNoTracking()
                .Where(p => p.Puan > 0
                    && (p.Kullanici.Aktif || (p.Kullanici.BanBitis != null && p.Kullanici.BanBitis <= simdi)));
            if (baslangic != null) q = q.Where(p => p.Tarih >= baslangic.Value);

            // Eşitlikte: puana en erken ulaşan (son puan kaydı daha eski) önde, sonra kullaniciId.
            var sirali = await q.GroupBy(p => p.KullaniciId)
                .Select(g => new { Id = g.Key, Puan = g.Sum(x => x.Puan), Son = g.Max(x => x.Tarih) })
                .OrderByDescending(x => x.Puan).ThenBy(x => x.Son).ThenBy(x => x.Id)
                .ToListAsync();

            var sonuc = new Siralama { Guncelleme = DateTime.UtcNow };
            var sira = 0;
            foreach (var x in sirali) sonuc.Kullanicilar[x.Id] = (++sira, x.Puan);

            var ust = sirali.Take(MaksLimit).ToList();
            var idler = ust.Select(x => x.Id).ToList();
            var adlar = await _context.Kullanicilar.AsNoTracking().Where(k => idler.Contains(k.Id))
                .Select(k => new { k.Id, k.KullaniciAdi, k.ProfilResmiURL }).ToDictionaryAsync(k => k.Id);
            Dictionary<int, int> toplamlar;
            if (baslangic == null) toplamlar = ust.ToDictionary(x => x.Id, x => x.Puan);
            else
                toplamlar = await _context.PuanKayitlari.AsNoTracking().Where(p => p.Puan > 0 && idler.Contains(p.KullaniciId))
                    .GroupBy(p => p.KullaniciId).Select(g => new { g.Key, T = g.Sum(x => x.Puan) })
                    .ToDictionaryAsync(x => x.Key, x => x.T);

            var stiller = await _tercihler.CozAsync(idler);
            var i = 0;
            foreach (var x in ust)
            {
                i++;
                if (!adlar.TryGetValue(x.Id, out var ad)) continue;
                sonuc.Ust.Add(new LiderlikSatiriDto
                {
                    Sira = i,
                    KullaniciId = x.Id,
                    KullaniciAdi = ad.KullaniciAdi,
                    ProfilResmiURL = ad.ProfilResmiURL,
                    Puan = x.Puan,
                    Rutbe = PuanServisi.Rutbe(toplamlar.TryGetValue(x.Id, out var t) ? t : x.Puan),
                    Seviye = PuanServisi.Seviye(toplamlar.TryGetValue(x.Id, out var t2) ? t2 : x.Puan),
                    AdStili = stiller.GetValueOrDefault(x.Id)
                });
            }

            _onbellek.Set(anahtar, sonuc, OnbellekSuresi);
            return sonuc;
        }
    }
}
