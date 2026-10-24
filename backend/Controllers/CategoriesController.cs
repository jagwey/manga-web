using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly IDenetimServisi _denetim;

        public CategoriesController(AppDbContext context, IDenetimServisi denetim)
        {
            _context = context;
            _denetim = denetim;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories()
        {
            var categories = await _context.Kategoriler
                .Include(k => k.MangaKategorileri)
                .OrderBy(k => k.KategoriAdi)
                .AsNoTracking()
                .Select(k => new CategoryDto
                {
                    Id = k.Id,
                    KategoriAdi = k.KategoriAdi,
                    MangaSayisi = k.MangaKategorileri.Count
                })
                .ToListAsync();

            return Ok(categories);
        }

        [Yetki("kategori.yonet")]
        [HttpPost]
        public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryRequest request)
        {
            var ad = (request?.KategoriAdi ?? string.Empty).Trim();
            var hata = Dogrula(ad);
            if (hata != null) return BadRequest(new { message = hata });

            if (await _context.Kategoriler.AnyAsync(k => k.KategoriAdi.ToLower() == ad.ToLower()))
                return BadRequest(new { message = "Bu isimde bir kategori zaten var." });

            var kategori = new Kategori { KategoriAdi = ad };
            _context.Kategoriler.Add(kategori);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "kategori.olustur", $"Kategori #{kategori.Id} {kategori.KategoriAdi}");

            return Ok(new CategoryDto { Id = kategori.Id, KategoriAdi = kategori.KategoriAdi, MangaSayisi = 0 });
        }

        [Yetki("kategori.yonet")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<CategoryDto>> Update(int id, [FromBody] CategoryRequest request)
        {
            var kategori = await _context.Kategoriler.FindAsync(id);
            if (kategori == null) return NotFound(new { message = "Kategori bulunamadı." });

            var ad = (request?.KategoriAdi ?? string.Empty).Trim();
            var hata = Dogrula(ad);
            if (hata != null) return BadRequest(new { message = hata });

            if (await _context.Kategoriler.AnyAsync(k => k.Id != id && k.KategoriAdi.ToLower() == ad.ToLower()))
                return BadRequest(new { message = "Bu isimde bir kategori zaten var." });

            var eskiAd = kategori.KategoriAdi;
            kategori.KategoriAdi = ad;
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "kategori.guncelle", $"Kategori #{id} {ad}", $"Eski ad: {eskiAd}");

            var sayi = await _context.MangaKategorileri.CountAsync(mk => mk.KategoriId == id);
            return Ok(new CategoryDto { Id = kategori.Id, KategoriAdi = kategori.KategoriAdi, MangaSayisi = sayi });
        }

        [Yetki("kategori.yonet")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var kategori = await _context.Kategoriler.FindAsync(id);
            if (kategori == null) return NotFound(new { message = "Kategori bulunamadı." });

            // MangaKategorileri kayıtları cascade ile silinir.
            _context.Kategoriler.Remove(kategori);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "kategori.sil", $"Kategori #{id} {kategori.KategoriAdi}");
            return Ok(new { message = "Kategori silindi." });
        }

        private static string? Dogrula(string ad)
        {
            if (ad.Length == 0) return "Kategori adı boş olamaz.";
            if (ad.Length > 50) return "Kategori adı en fazla 50 karakter olabilir.";
            return null;
        }
    }
}
