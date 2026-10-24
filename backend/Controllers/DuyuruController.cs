using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    public class DuyuruYazRequest
    {
        public string? Baslik { get; set; }
        public string? Etiket { get; set; }
        public string? Baglanti { get; set; }
        public bool? Aktif { get; set; }
        public int? Sira { get; set; }
        public DateTimeOffset? BitisTarihi { get; set; }
        /// <summary>true ise mevcut bitiş tarihi kaldırılır (PUT).</summary>
        public bool? BitisiKaldir { get; set; }
    }

    [ApiController]
    [Route("api")]
    public class DuyuruController : ControllerBase
    {
        private static readonly string[] Etiketler = { "bilgi", "etkinlik", "yeni", "uyari" };
        private readonly AppDbContext _context;
        private readonly IDenetimServisi _denetim;

        public DuyuruController(AppDbContext context, IDenetimServisi denetim)
        {
            _context = context;
            _denetim = denetim;
        }

        private static object Dto(Duyuru d) => new
        {
            id = d.Id, baslik = d.Baslik, etiket = d.Etiket, baglanti = d.Baglanti,
            aktif = d.Aktif, sira = d.Sira, tarih = d.Tarih, bitisTarihi = d.BitisTarihi
        };

        /// <summary>Herkese açık: aktif ve süresi geçmemiş duyurular (en fazla 12).</summary>
        [HttpGet("duyurular")]
        public async Task<IActionResult> Liste()
        {
            var simdi = DateTime.UtcNow;
            var liste = await _context.Duyurular.AsNoTracking()
                .Where(d => d.Aktif && (d.BitisTarihi == null || d.BitisTarihi > simdi))
                .OrderBy(d => d.Sira).ThenByDescending(d => d.Tarih).ThenByDescending(d => d.Id)
                .Take(12).ToListAsync();
            return Ok(liste.Select(d => new { id = d.Id, baslik = d.Baslik, etiket = d.Etiket, baglanti = d.Baglanti, tarih = d.Tarih }));
        }

        [Yetki("duyuru.yonet")]
        [HttpGet("admin/duyurular")]
        public async Task<IActionResult> AdminListe()
        {
            var liste = await _context.Duyurular.AsNoTracking()
                .OrderBy(d => d.Sira).ThenByDescending(d => d.Tarih).ThenByDescending(d => d.Id).ToListAsync();
            return Ok(liste.Select(Dto));
        }

        [Yetki("duyuru.yonet")]
        [HttpPost("admin/duyurular")]
        public async Task<IActionResult> Ekle([FromBody] DuyuruYazRequest? istek)
        {
            if (istek == null) return BadRequest(new { message = "Geçersiz istek." });
            var d = new Duyuru { Tarih = DateTime.UtcNow, Aktif = true, Etiket = "bilgi" };
            var hata = Uygula(d, istek, yeni: true);
            if (hata != null) return BadRequest(new { message = hata });
            _context.Duyurular.Add(d);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "duyuru.olustur", $"Duyuru #{d.Id}", d.Baslik);
            return Ok(Dto(d));
        }

        [Yetki("duyuru.yonet")]
        [HttpPut("admin/duyurular/{id:int}")]
        public async Task<IActionResult> Guncelle(int id, [FromBody] DuyuruYazRequest? istek)
        {
            if (istek == null) return BadRequest(new { message = "Geçersiz istek." });
            var d = await _context.Duyurular.FirstOrDefaultAsync(x => x.Id == id);
            if (d == null) return NotFound(new { message = "Duyuru bulunamadı." });
            var hata = Uygula(d, istek, yeni: false);
            if (hata != null) return BadRequest(new { message = hata });
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "duyuru.guncelle", $"Duyuru #{d.Id}", d.Baslik);
            return Ok(Dto(d));
        }

        [Yetki("duyuru.yonet")]
        [HttpDelete("admin/duyurular/{id:int}")]
        public async Task<IActionResult> Sil(int id)
        {
            var d = await _context.Duyurular.FirstOrDefaultAsync(x => x.Id == id);
            if (d == null) return NotFound(new { message = "Duyuru bulunamadı." });
            _context.Duyurular.Remove(d);
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "duyuru.sil", $"Duyuru #{id}", d.Baslik);
            return Ok(new { message = "Duyuru silindi." });
        }

        /// <summary>Hata mesajı ya da null. Verilen alanlar uygulanır; null olanlar (güncellemede) değişmez.</summary>
        private static string? Uygula(Duyuru d, DuyuruYazRequest r, bool yeni)
        {
            if (r.Baslik != null || yeni)
            {
                var b = (r.Baslik ?? string.Empty).Trim();
                if (b.Length == 0) return "Duyuru başlığı boş bırakılamaz.";
                if (b.Length > 150) return "Duyuru başlığı en fazla 150 karakter olabilir.";
                if (b.Any(char.IsControl)) return "Başlık denetim karakteri içeremez.";
                d.Baslik = b;
            }
            if (r.Etiket != null)
            {
                var e = r.Etiket.Trim().ToLowerInvariant();
                if (!Etiketler.Contains(e)) return "Etiket 'bilgi', 'etkinlik', 'yeni' ya da 'uyari' olmalıdır.";
                d.Etiket = e;
            }
            if (r.Baglanti != null)
            {
                var l = r.Baglanti.Trim();
                if (l.Length == 0) d.Baglanti = null;
                else
                {
                    var h = BaglantiHatasi(l);
                    if (h != null) return h;
                    d.Baglanti = l;
                }
            }
            if (r.Aktif.HasValue) d.Aktif = r.Aktif.Value;
            if (r.Sira.HasValue)
            {
                if (r.Sira.Value < -10000 || r.Sira.Value > 10000) return "Sıra -10000 ile 10000 arasında olmalıdır.";
                d.Sira = r.Sira.Value;
            }
            if (r.BitisiKaldir == true) d.BitisTarihi = null;
            else if (r.BitisTarihi.HasValue) d.BitisTarihi = r.BitisTarihi.Value.UtcDateTime;
            return null;
        }

        /// <summary>http/https, "#/..." ya da "/..." ile başlamalı; javascript: ve benzeri şemalar yasak.</summary>
        public static string? BaglantiHatasi(string l)
        {
            if (l.Length > 300) return "Bağlantı en fazla 300 karakter olabilir.";
            if (l.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))) return "Bağlantı boşluk veya denetim karakteri içeremez.";
            if (l.StartsWith("#/", StringComparison.Ordinal)) return null;
            if (l.StartsWith("/", StringComparison.Ordinal) && !l.StartsWith("//", StringComparison.Ordinal) && !l.Contains('\\')) return null;
            if (Uri.TryCreate(l, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
                && (l.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return null;
            return "Bağlantı http/https, \"#/...\" ya da \"/...\" ile başlamalıdır.";
        }
    }
}
