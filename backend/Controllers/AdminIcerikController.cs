using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    /// <summary>Yönetim paneli içerik uçları: seri listesi, sayfa yönetimi, istatistik.</summary>
    [ApiController]
    [Route("api/admin")]
    public class AdminIcerikController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IDenetimServisi _denetim;

        public AdminIcerikController(AppDbContext context, IWebHostEnvironment env, IDenetimServisi denetim)
        {
            _context = context;
            _env = env;
            _denetim = denetim;
        }

        // ---------- Seri listesi ----------

        [Yetki("manga.yaz", "manga.sil", "bolum.yaz", "bolum.sil", "bolum.duzenle")]
        [HttpGet("mangalar")]
        public async Task<ActionResult<IEnumerable<AdminMangaOzetDto>>> Mangalar(
            [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Mangalar.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(m => m.MangaAdi.ToLower().Contains(term));
            }

            var toplam = await query.CountAsync();
            var liste = await query
                .OrderByDescending(m => m.GuncellenmeTarihi).ThenByDescending(m => m.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(m => new AdminMangaOzetDto
                {
                    Id = m.Id,
                    MangaAdi = m.MangaAdi,
                    KapakResmiURL = m.KapakResmiURL,
                    Durum = m.Durum,
                    Yayinda = m.Yayinda,
                    OneCikan = m.OneCikan,
                    BolumSayisi = m.Bolumler.Count,
                    EklenmeTarihi = m.EklenmeTarihi,
                    GuncellenmeTarihi = m.GuncellenmeTarihi
                })
                .ToListAsync();

            Response.Headers.Append("X-Total-Count", toplam.ToString());
            return Ok(liste);
        }

        // ---------- Sayfa yönetimi ----------

        [Yetki("bolum.duzenle", "bolum.yaz")]
        [HttpGet("chapters/{id:int}/sayfalar")]
        public async Task<ActionResult<AdminBolumSayfalarDto>> Sayfalar(int id)
        {
            var dto = await SayfalariGetirAsync(id);
            if (dto == null) return NotFound(new { message = "Bölüm bulunamadı." });
            return Ok(dto);
        }

        [Yetki("bolum.duzenle")]
        [HttpPut("chapters/{id:int}/sayfalar/siralama")]
        public async Task<ActionResult<AdminBolumSayfalarDto>> Siralama(int id, [FromBody] SayfaSiralamaRequest request)
        {
            var bolum = await BolumBilgisiAsync(id);
            if (bolum == null) return NotFound(new { message = "Bölüm bulunamadı." });

            var istenen = request?.Idler;
            if (istenen == null) return BadRequest(new { message = "Sayfa kimlik listesi (idler) zorunludur." });
            if (istenen.Distinct().Count() != istenen.Count)
                return BadRequest(new { message = "Sayfa kimlikleri benzersiz olmalıdır." });

            var mevcut = await _context.Sayfalar.AsNoTracking().Where(s => s.BolumId == id).Select(s => s.Id).ToListAsync();
            if (mevcut.Count != istenen.Count || !mevcut.ToHashSet().SetEquals(istenen))
                return BadRequest(new { message = "Liste, bölümün tüm sayfa kimliklerini tam olarak içermelidir." });

            await YenidenNumaralaAsync(id, istenen);
            await MangaGuncellendiAsync(bolum.MangaId);
            await _denetim.KaydetAsync(User, "sayfa.siralama", $"Bölüm #{id} ({bolum.MangaAdi} {bolum.BolumNo})", $"{istenen.Count} sayfa yeniden sıralandı");

            return Ok(await SayfalariGetirAsync(id));
        }

        [Yetki("bolum.duzenle", "bolum.yaz")]
        [HttpPost("chapters/{id:int}/sayfalar")]
        public async Task<ActionResult<AdminBolumSayfalarDto>> SayfaEkle(int id, [FromBody] SayfaEkleRequest request)
        {
            var bolum = await BolumBilgisiAsync(id);
            if (bolum == null) return NotFound(new { message = "Bölüm bulunamadı." });

            var urller = (request?.Urller ?? new List<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList();
            if (urller.Count == 0) return BadRequest(new { message = "Eklenecek en az bir görsel adresi gerekli." });
            if (urller.Count > 200) return BadRequest(new { message = "Tek seferde en fazla 200 sayfa eklenebilir." });
            foreach (var u in urller)
            {
                var hata = UrlHatasi(u);
                if (hata != null) return BadRequest(new { message = hata });
            }

            var son = await _context.Sayfalar.Where(s => s.BolumId == id).MaxAsync(s => (int?)s.SayfaNo) ?? 0;
            foreach (var u in urller)
            {
                _context.Sayfalar.Add(new Sayfa { BolumId = id, SayfaNo = ++son, ResimURL = u });
            }
            await _context.SaveChangesAsync();
            await MangaGuncellendiAsync(bolum.MangaId);
            await _denetim.KaydetAsync(User, "sayfa.ekle", $"Bölüm #{id} ({bolum.MangaAdi} {bolum.BolumNo})", $"{urller.Count} sayfa eklendi");

            return Ok(await SayfalariGetirAsync(id));
        }

        [Yetki("bolum.duzenle")]
        [HttpPut("sayfalar/{id:int}")]
        public async Task<ActionResult<AdminSayfaDto>> SayfaDegistir(int id, [FromBody] SayfaGuncelleRequest request)
        {
            var sayfa = await _context.Sayfalar.Include(s => s.Bolum).ThenInclude(b => b.Manga).FirstOrDefaultAsync(s => s.Id == id);
            if (sayfa == null) return NotFound(new { message = "Sayfa bulunamadı." });

            var yeni = (request?.ResimURL ?? string.Empty).Trim();
            var hata = UrlHatasi(yeni);
            if (hata != null) return BadRequest(new { message = hata });

            var eski = sayfa.ResimURL;
            sayfa.ResimURL = yeni;
            sayfa.Bolum.Manga.GuncellenmeTarihi = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (!string.Equals(eski, yeni, StringComparison.Ordinal))
                await UploadDosyaServisi.TemizleAsync(_context, _env, new[] { eski });

            await _denetim.KaydetAsync(User, "sayfa.degistir",
                $"Sayfa #{id} ({sayfa.Bolum.Manga.MangaAdi} {sayfa.Bolum.BolumNo}, s.{sayfa.SayfaNo})");

            return Ok(new AdminSayfaDto { Id = sayfa.Id, SayfaNo = sayfa.SayfaNo, ResimURL = sayfa.ResimURL });
        }

        [Yetki("bolum.duzenle")]
        [HttpDelete("sayfalar/{id:int}")]
        public async Task<ActionResult> SayfaSil(int id)
        {
            var sayfa = await _context.Sayfalar.AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new { s.Id, s.BolumId, s.SayfaNo, s.ResimURL, s.Bolum.MangaId, MangaAdi = s.Bolum.Manga.MangaAdi, s.Bolum.BolumNo })
                .FirstOrDefaultAsync();
            if (sayfa == null) return NotFound(new { message = "Sayfa bulunamadı." });

            await using (var tx = await _context.Database.BeginTransactionAsync())
            {
                await _context.Sayfalar.Where(s => s.Id == id).ExecuteDeleteAsync();
                var kalan = await _context.Sayfalar.AsNoTracking().Where(s => s.BolumId == sayfa.BolumId)
                    .OrderBy(s => s.SayfaNo).ThenBy(s => s.Id).Select(s => s.Id).ToListAsync();
                await NumaralaAsync(sayfa.BolumId, kalan);
                await tx.CommitAsync();
            }
            await MangaGuncellendiAsync(sayfa.MangaId);
            await UploadDosyaServisi.TemizleAsync(_context, _env, new[] { sayfa.ResimURL });
            await _denetim.KaydetAsync(User, "sayfa.sil", $"Sayfa #{id} ({sayfa.MangaAdi} {sayfa.BolumNo}, s.{sayfa.SayfaNo})");

            return Ok(new { message = "Sayfa silindi." });
        }

        // ---------- İstatistik ----------

        [Yetki("manga.yaz", "manga.sil", "bolum.yaz", "bolum.sil", "bolum.duzenle", "kategori.yonet", "yorum.sil",
            "kullanici.gor", "kullanici.engelle", "kullanici.duzenle", "kullanici.sifre", "kullanici.sil", "kayit.gor", "ayar.yonet")]
        [HttpGet("istatistik")]
        public async Task<ActionResult<IstatistikDto>> Istatistik([FromQuery] int gun = 14)
        {
            if (gun < 1) gun = 1;
            if (gun > 90) gun = 90;

            var bugun = DateTime.UtcNow.Date;
            var baslangic = bugun.AddDays(-(gun - 1));

            var kullanicilar = await _context.Kullanicilar.AsNoTracking().Where(k => k.KayitTarihi >= baslangic)
                .Select(k => k.KayitTarihi).ToListAsync();
            var yorumlar = await _context.Yorumlar.AsNoTracking().Where(y => y.Tarih >= baslangic)
                .Select(y => y.Tarih).ToListAsync();
            var bolumler = await _context.Bolumler.AsNoTracking().Where(b => b.YayinTarihi >= baslangic)
                .Select(b => b.YayinTarihi).ToListAsync();

            static Dictionary<DateTime, int> Say(List<DateTime> l) =>
                l.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Count());
            var kd = Say(kullanicilar);
            var yd = Say(yorumlar);
            var bd = Say(bolumler);

            var sonuc = new IstatistikDto();
            for (int i = 0; i < gun; i++)
            {
                var d = baslangic.AddDays(i);
                sonuc.Gunler.Add(new IstatistikGunDto
                {
                    Tarih = d.ToString("yyyy-MM-dd"),
                    YeniKullanici = kd.GetValueOrDefault(d),
                    YeniYorum = yd.GetValueOrDefault(d),
                    YeniBolum = bd.GetValueOrDefault(d)
                });
            }
            sonuc.Toplam.YeniKullanici = sonuc.Gunler.Sum(x => x.YeniKullanici);
            sonuc.Toplam.YeniYorum = sonuc.Gunler.Sum(x => x.YeniYorum);
            sonuc.Toplam.YeniBolum = sonuc.Gunler.Sum(x => x.YeniBolum);
            return Ok(sonuc);
        }

        // ---------- Yardımcılar ----------

        private static string? UrlHatasi(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "Görsel adresi boş olamaz.";
            if (url.Length > 500) return "Görsel adresi en fazla 500 karakter olabilir.";
            if (url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
                return "Görsel adresi boşluk veya denetim karakteri içeremez.";
            if (url.StartsWith("/uploads/", StringComparison.Ordinal))
            {
                if (url.Contains("..") || url.Contains('\\') || url.Length <= "/uploads/".Length)
                    return "Geçersiz görsel adresi.";
                return null;
            }
            if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                return null;
            return "Görsel adresi http/https ile başlamalı ya da /uploads/ altında olmalıdır.";
        }

        private sealed record BolumBilgi(int MangaId, string MangaAdi, decimal BolumNo);

        private async Task<BolumBilgi?> BolumBilgisiAsync(int id)
        {
            var b = await _context.Bolumler.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.MangaId, MangaAdi = x.Manga.MangaAdi, x.BolumNo }).FirstOrDefaultAsync();
            return b == null ? null : new BolumBilgi(b.MangaId, b.MangaAdi, b.BolumNo);
        }

        private async Task<AdminBolumSayfalarDto?> SayfalariGetirAsync(int bolumId)
        {
            var dto = await _context.Bolumler.AsNoTracking().Where(b => b.Id == bolumId)
                .Select(b => new AdminBolumSayfalarDto
                {
                    BolumId = b.Id,
                    MangaId = b.MangaId,
                    MangaAdi = b.Manga.MangaAdi,
                    BolumNo = b.BolumNo,
                    BolumAdi = b.BolumAdi
                }).FirstOrDefaultAsync();
            if (dto == null) return null;
            dto.Sayfalar = await _context.Sayfalar.AsNoTracking().Where(s => s.BolumId == bolumId)
                .OrderBy(s => s.SayfaNo).ThenBy(s => s.Id)
                .Select(s => new AdminSayfaDto { Id = s.Id, SayfaNo = s.SayfaNo, ResimURL = s.ResimURL })
                .ToListAsync();
            return dto;
        }

        private Task MangaGuncellendiAsync(int mangaId) =>
            _context.Mangalar.Where(m => m.Id == mangaId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.GuncellenmeTarihi, DateTime.UtcNow));

        /// <summary>Tek işlemde, verilen sırayla 1..n numaralandırır.</summary>
        private async Task YenidenNumaralaAsync(int bolumId, List<int> siraliIdler)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            await NumaralaAsync(bolumId, siraliIdler);
            await tx.CommitAsync();
        }

        /// <summary>
        /// Benzersiz (BolumId, SayfaNo) indeksine takılmamak için önce tüm numaraları negatife çevirir
        /// (pozitifler benzersizdi, negatifler de olur), sonra son numaraları yazar. Çağıran işlem sahibidir.
        /// </summary>
        private async Task NumaralaAsync(int bolumId, List<int> siraliIdler)
        {
            await _context.Sayfalar.Where(s => s.BolumId == bolumId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SayfaNo, p => -p.SayfaNo));
            for (int i = 0; i < siraliIdler.Count; i++)
            {
                var sid = siraliIdler[i];
                var no = i + 1;
                await _context.Sayfalar.Where(s => s.Id == sid && s.BolumId == bolumId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.SayfaNo, no));
            }
        }
    }
}
