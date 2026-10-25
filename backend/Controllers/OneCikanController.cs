using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api")]
    public class OneCikanController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OneCikanController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>Herkese açık: öne çıkan işaretli yayındaki seriler (en fazla 8).</summary>
        [HttpGet("one-cikanlar")]
        public async Task<IActionResult> Liste()
        {
            var seriler = await _context.Mangalar.AsNoTracking()
                .Where(m => m.OneCikan && m.Yayinda)
                .OrderByDescending(m => m.GuncellenmeTarihi).ThenByDescending(m => m.Id)
                .Take(8)
                .Select(m => new
                {
                    m.Id, m.MangaAdi, m.Aciklama, m.KapakResmiURL, m.OneCikanResimURL,
                    Kategori = m.MangaKategorileri.Select(mk => mk.Kategori.KategoriAdi).FirstOrDefault(),
                    BolumSayisi = m.Bolumler.Count(),
                    IlkBolumId = m.Bolumler.OrderBy(b => b.BolumNo).Select(b => (int?)b.Id).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(seriler.Select(m => new
            {
                id = "m" + m.Id,
                baslik = m.MangaAdi,
                ozet = Kisalt(m.Aciklama, 200),
                etiket = m.Kategori ?? "Manga",
                gorselUrl = m.OneCikanResimURL ?? m.KapakResmiURL,
                baglanti = $"#/manga/{m.Id}",
                kapakUrl = m.KapakResmiURL,
                bolumSayisi = m.BolumSayisi,
                ilkBolumId = m.IlkBolumId,
                genis = m.OneCikanResimURL != null
            }));
        }

        private static string? Kisalt(string? metin, int azami)
        {
            if (string.IsNullOrWhiteSpace(metin)) return null;
            var t = metin.Trim();
            return t.Length <= azami ? t : t[..azami].TrimEnd() + "…";
        }
    }
}
