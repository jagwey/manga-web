using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api")]
    public class OneCikanController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDenetimServisi _denetim;

        public OneCikanController(AppDbContext context, IDenetimServisi denetim)
        {
            _context = context;
            _denetim = denetim;
        }

        private static object Dto(OneCikan o) => new
        {
            id = o.Id, baslik = o.Baslik, ozet = o.Ozet, etiket = o.Etiket, gorselUrl = o.GorselUrl, baglanti = o.Baglanti,
            aktif = o.Aktif, sira = o.Sira, tarih = o.Tarih, bitisTarihi = o.BitisTarihi
        };

        /// <summary>Herkese açık: aktif ve süresi geçmemiş öne çıkanlar (en fazla 8).</summary>
        [HttpGet("one-cikanlar")]
        public async Task<IActionResult> Liste()
        {
            var simdi = DateTime.UtcNow;
            var liste = await _context.OneCikanlar.AsNoTracking()
                .Where(o => o.Aktif && (o.BitisTarihi == null || o.BitisTarihi > simdi))
                .OrderBy(o => o.Sira).ThenByDescending(o => o.Tarih).ThenByDescending(o => o.Id)
                .Take(8).ToListAsync();
            return Ok(liste.Select(o => new { id = o.Id, baslik = o.Baslik, ozet = o.Ozet, etiket = o.Etiket, gorselUrl = o.GorselUrl, baglanti = o.Baglanti }));
        }

        [Yetki("duyuru.yonet")]
        [HttpGet("admin/one-cikanlar")]
        public async Task<IActionResult> AdminListe()
        {
            var liste = await _context.OneCikanlar.AsNoTracking()
                .OrderBy(o => o.Sira).ThenByDescending(o => o.Tarih).ThenByDescending(o => o.Id).ToListAsync();
            return Ok(liste.Select(Dto));
        }

        [Yetki("duyuru.yonet")]
        [HttpPost("admin/one-cikanlar")]
        public async Task<IActionResult> Ekle([FromBody] OneCikanYazRequest? istek)
        {
            if (istek == null) return BadRequest(new { message = "Geçersiz istek." });
            var o = new OneCikan { Tarih = DateTime.UtcNow, Aktif = true, Etiket = "Haber" };
            var hata = Uygula(o, istek, yeni: true);
            if (hata != null) return BadRequest(new { message = hata });
            _context.OneCikanlar.Add(o);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "onecikan.olustur", $"Öne çıkan #{o.Id}", o.Baslik);
            return Ok(Dto(o));
        }

        [Yetki("duyuru.yonet")]
        [HttpPut("admin/one-cikanlar/{id:int}")]
        public async Task<IActionResult> Guncelle(int id, [FromBody] OneCikanYazRequest? istek)
        {
            if (istek == null) return BadRequest(new { message = "Geçersiz istek." });
            var o = await _context.OneCikanlar.FirstOrDefaultAsync(x => x.Id == id);
            if (o == null) return NotFound(new { message = "Öne çıkan kayıt bulunamadı." });
            var hata = Uygula(o, istek, yeni: false);
            if (hata != null) return BadRequest(new { message = hata });
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "onecikan.guncelle", $"Öne çıkan #{o.Id}", o.Baslik);
            return Ok(Dto(o));
        }

        [Yetki("duyuru.yonet")]
        [HttpDelete("admin/one-cikanlar/{id:int}")]
        public async Task<IActionResult> Sil(int id)
        {
            var o = await _context.OneCikanlar.FirstOrDefaultAsync(x => x.Id == id);
            if (o == null) return NotFound(new { message = "Öne çıkan kayıt bulunamadı." });
            _context.OneCikanlar.Remove(o);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "onecikan.sil", $"Öne çıkan #{id}", o.Baslik);
            return Ok(new { message = "Öne çıkan kayıt silindi." });
        }

        /// <summary>Hata mesajı ya da null. Verilen alanlar uygulanır; null olanlar (güncellemede) değişmez.</summary>
        private static string? Uygula(OneCikan o, OneCikanYazRequest r, bool yeni)
        {
            if (r.Baslik != null || yeni)
            {
                var b = (r.Baslik ?? string.Empty).Trim();
                if (b.Length == 0) return "Başlık boş bırakılamaz.";
                if (b.Length > 150) return "Başlık en fazla 150 karakter olabilir.";
                if (b.Any(char.IsControl)) return "Başlık denetim karakteri içeremez.";
                o.Baslik = b;
            }
            if (r.Ozet != null)
            {
                var z = r.Ozet.Trim();
                if (z.Length > 400) return "Özet en fazla 400 karakter olabilir.";
                if (z.Any(c => char.IsControl(c) && c != '\n' && c != '\r')) return "Özet denetim karakteri içeremez.";
                o.Ozet = z.Length == 0 ? null : z;
            }
            if (r.Etiket != null)
            {
                var e = r.Etiket.Trim();
                if (e.Length == 0) return "Etiket boş bırakılamaz.";
                if (e.Length > 30) return "Etiket en fazla 30 karakter olabilir.";
                if (e.Any(char.IsControl)) return "Etiket denetim karakteri içeremez.";
                o.Etiket = e;
            }
            if (r.GorselUrl != null)
            {
                var g = r.GorselUrl.Trim();
                if (g.Length == 0) o.GorselUrl = null;
                else
                {
                    var h = UrlHatasi(g, 500, "Görsel adresi");
                    if (h != null) return h;
                    o.GorselUrl = g;
                }
            }
            if (r.Baglanti != null)
            {
                var l = r.Baglanti.Trim();
                if (l.Length == 0) o.Baglanti = null;
                else
                {
                    var h = UrlHatasi(l, 300, "Bağlantı");
                    if (h != null) return h;
                    o.Baglanti = l;
                }
            }
            if (r.Aktif.HasValue) o.Aktif = r.Aktif.Value;
            if (r.Sira.HasValue)
            {
                if (r.Sira.Value < -10000 || r.Sira.Value > 10000) return "Sıra -10000 ile 10000 arasında olmalıdır.";
                o.Sira = r.Sira.Value;
            }
            if (r.BitisiKaldir == true) o.BitisTarihi = null;
            else if (r.BitisTarihi.HasValue) o.BitisTarihi = r.BitisTarihi.Value.UtcDateTime;
            return null;
        }

        /// <summary>Yalnız "/..." (tek eğik çizgi), http:// ya da https:// ile başlayabilir; javascript: ve benzeri şemalar yasak.</summary>
        private static string? UrlHatasi(string u, int azami, string ad)
        {
            if (u.Length > azami) return $"{ad} en fazla {azami} karakter olabilir.";
            if (u.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))) return $"{ad} boşluk veya denetim karakteri içeremez.";
            if (u.StartsWith("/", StringComparison.Ordinal) && !u.StartsWith("//", StringComparison.Ordinal) && !u.Contains('\\')) return null;
            if (Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return null;
            return $"{ad} http://, https:// ya da \"/\" ile başlamalıdır.";
        }
    }
}
