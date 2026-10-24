using Microsoft.Extensions.Caching.Memory;
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
    [Route("api/[controller]")]
    public class MangasController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IDenetimServisi _denetim;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _onbellek;

        public MangasController(AppDbContext context, IWebHostEnvironment env, IDenetimServisi denetim, Microsoft.Extensions.Caching.Memory.IMemoryCache onbellek)
        {
            _onbellek = onbellek;
            _context = context;
            _env = env;
            _denetim = denetim;
        }

        /// <summary>
        /// Manga listesi (Arama, Kategori Filtresi ve Sayfalama destekli)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MangaListDto>>> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] bool onecikan = false,
            [FromQuery] string? sirala = null,
            [FromQuery] string? durum = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (page > 100000) page = 100000; // (page-1)*pageSize int taşmasın
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            // Aşırı uzun metinler SQL kolon sınırını aşıp 500 üretmesin.
            if (search != null && search.Length > 100) search = search[..100];
            if (category != null && category.Length > 300) category = category[..300];

            var query = _context.Mangalar.AsNoTracking().AsQueryable();

            if (!await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context))
            {
                query = query.Where(m => m.Yayinda);
            }

            if (onecikan)
            {
                query = query.Where(m => m.OneCikan);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(m => m.MangaAdi.ToLower().Contains(term) ||
                                         (m.Aciklama != null && m.Aciklama.ToLower().Contains(term)) ||
                                         (m.Yazar != null && m.Yazar.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var secilenKategoriler = category.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(k => k.ToLower()).Distinct().Take(10).ToList();
                foreach (var catTerm in secilenKategoriler)
                {
                    var k = catTerm;
                    query = query.Where(m => m.MangaKategorileri.Any(mk => mk.Kategori.KategoriAdi.ToLower() == k));
                }
            }

            if (!string.IsNullOrWhiteSpace(durum))
            {
                var d = durum.Trim();
                query = query.Where(m => m.Durum == d);
            }

            var totalCount = await query.CountAsync();

            // sirala: yeni (varsayılan), puan, ad. Eşitlikte sıra sabit kalsın diye Id ile bağlanır.
            IOrderedQueryable<Manga> sirali = (sirala ?? "").Trim().ToLowerInvariant() switch
            {
                "puan" => query.OrderByDescending(m => _context.MangaPuanlari.Where(p => p.MangaId == m.Id).Average(p => (decimal?)p.Deger)).ThenByDescending(m => m.Id),
                "ad" => query.OrderBy(m => m.MangaAdi).ThenBy(m => m.Id),
                _ => query.OrderByDescending(m => m.EklenmeTarihi).ThenByDescending(m => m.Id)
            };

            var mangas = await sirali
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MangaListDto
                {
                    Id = m.Id,
                    MangaAdi = m.MangaAdi,
                    Aciklama = m.Aciklama,
                    KapakResmiURL = m.KapakResmiURL,
                    Yazar = m.Yazar,
                    Cizer = m.Cizer,
                    Durum = m.Durum,
                    EklenmeTarihi = m.EklenmeTarihi,
                    Puan = _context.MangaPuanlari.Where(p => p.MangaId == m.Id).Average(p => (decimal?)p.Deger),
                    OySayisi = _context.MangaPuanlari.Count(p => p.MangaId == m.Id),
                    OkunmaSayisi = m.OkunmaSayisi,
                    Kategoriler = m.MangaKategorileri.Select(mk => mk.Kategori.KategoriAdi).ToList(),
                    BolumSayisi = m.Bolumler.Count,
                    Yayinda = m.Yayinda,
                    OneCikan = m.OneCikan,
                    IlkBolumId = m.Bolumler.OrderBy(b => b.BolumNo).Select(b => (int?)b.Id).FirstOrDefault(),
                    SonBolumId = m.Bolumler.OrderByDescending(b => b.BolumNo).Select(b => (int?)b.Id).FirstOrDefault()
                })
                .ToListAsync();

            foreach (var mg in mangas) mg.Puan = mg.Puan.HasValue ? Math.Round(mg.Puan.Value, 1, MidpointRounding.AwayFromZero) : null;

            Response.Headers.Append("X-Total-Count", totalCount.ToString());

            return Ok(mangas);
        }

        /// <summary>Yayındaki serilerden rastgele birini döner.</summary>
        [HttpGet("rastgele")]
        public async Task<IActionResult> Rastgele()
        {
            var idler = await _context.Mangalar.AsNoTracking().Where(m => m.Yayinda).Select(m => new { m.Id, m.MangaAdi }).ToListAsync();
            if (idler.Count == 0) return NotFound(new { message = "Henüz yayında seri yok." });
            var secilen = idler[System.Security.Cryptography.RandomNumberGenerator.GetInt32(idler.Count)];
            return Ok(new { id = secilen.Id, mangaAdi = secilen.MangaAdi });
        }

        /// <summary>
        /// Popüler seriler (anonim, gizli seri hariç). donem: hafta (7 gün) | ay (30 gün) | tum.
        /// Okunma = OkumaGecmisi'nde dönemde güncellenen FARKLI kullanıcı sayısı (tabloda kullanıcı+seri başına tek satır var,
        /// bu yüzden satır sayısı = farklı okuyucu sayısı; indeksli ve ucuz). 60 sn bellek önbelleği.
        /// </summary>
        [HttpGet("populer")]
        public async Task<ActionResult<IEnumerable<MangaListDto>>> Populer([FromQuery] string? donem = "hafta", [FromQuery] int limit = 10)
        {
            donem = (donem ?? "hafta").Trim().ToLowerInvariant();
            if (donem != "hafta" && donem != "ay" && donem != "tum")
                return BadRequest(new { message = "donem 'hafta', 'ay' ya da 'tum' olmalıdır." });
            if (limit < 1) limit = 10;
            if (limit > 50) limit = 50;

            var anahtar = $"populer:{donem}:{limit}";
            if (_onbellek.TryGetValue(anahtar, out List<MangaListDto>? hazir) && hazir != null) return Ok(hazir);

            DateTime? since = donem == "hafta" ? DateTime.UtcNow.AddDays(-7) : donem == "ay" ? DateTime.UtcNow.AddDays(-30) : null;
            var liste = await _context.Mangalar.AsNoTracking().Where(m => m.Yayinda)
                .Select(m => new
                {
                    M = m,
                    Okunma = _context.OkumaGecmisi.Count(o => o.MangaId == m.Id && (since == null || o.GuncellenmeTarihi >= since))
                })
                .OrderByDescending(x => x.Okunma).ThenByDescending(x => x.M.EklenmeTarihi).ThenByDescending(x => x.M.Id)
                .Take(limit)
                .Select(x => new MangaListDto
                {
                    Id = x.M.Id,
                    MangaAdi = x.M.MangaAdi,
                    Aciklama = x.M.Aciklama,
                    KapakResmiURL = x.M.KapakResmiURL,
                    Yazar = x.M.Yazar,
                    Cizer = x.M.Cizer,
                    Durum = x.M.Durum,
                    EklenmeTarihi = x.M.EklenmeTarihi,
                    Puan = _context.MangaPuanlari.Where(p => p.MangaId == x.M.Id).Average(p => (decimal?)p.Deger),
                    OySayisi = _context.MangaPuanlari.Count(p => p.MangaId == x.M.Id),
                    OkunmaSayisi = x.M.OkunmaSayisi,
                    Kategoriler = x.M.MangaKategorileri.Select(mk => mk.Kategori.KategoriAdi).ToList(),
                    BolumSayisi = x.M.Bolumler.Count,
                    Yayinda = x.M.Yayinda,
                    OneCikan = x.M.OneCikan,
                    IlkBolumId = x.M.Bolumler.OrderBy(b => b.BolumNo).Select(b => (int?)b.Id).FirstOrDefault(),
                    SonBolumId = x.M.Bolumler.OrderByDescending(b => b.BolumNo).Select(b => (int?)b.Id).FirstOrDefault(),
                    Okunma = x.Okunma
                })
                .ToListAsync();
            foreach (var mg in liste) mg.Puan = mg.Puan.HasValue ? Math.Round(mg.Puan.Value, 1, MidpointRounding.AwayFromZero) : null;

            _onbellek.Set(anahtar, liste, TimeSpan.FromSeconds(60));
            return Ok(liste);
        }

        /// <summary>
        /// Tekil manga detayları ve bölümler listesi
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<MangaDetailDto>> GetById(int id)
        {
            var gizliGorur = await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context);
            var result = await _context.Mangalar
                .AsNoTracking()
                .Where(m => m.Id == id && (m.Yayinda || gizliGorur))
                .Select(m => new MangaDetailDto
                {
                    Id = m.Id,
                    MangaAdi = m.MangaAdi,
                    Aciklama = m.Aciklama,
                    KapakResmiURL = m.KapakResmiURL,
                    Yazar = m.Yazar,
                    Cizer = m.Cizer,
                    Durum = m.Durum,
                    EklenmeTarihi = m.EklenmeTarihi,
                    Puan = _context.MangaPuanlari.Where(p => p.MangaId == m.Id).Average(p => (decimal?)p.Deger),
                    OySayisi = _context.MangaPuanlari.Count(p => p.MangaId == m.Id),
                    OkunmaSayisi = m.OkunmaSayisi,
                    Kategoriler = m.MangaKategorileri.Select(mk => mk.Kategori.KategoriAdi).ToList(),
                    Yayinda = m.Yayinda,
                    OneCikan = m.OneCikan,
                    Bolumler = m.Bolumler
                        .OrderBy(b => b.BolumNo)
                        .Select(b => new BolumSummaryDto
                        {
                            Id = b.Id,
                            BolumNo = b.BolumNo,
                            BolumAdi = b.BolumAdi,
                            YayinTarihi = b.YayinTarihi,
                            SayfaSayisi = b.Sayfalar.Count()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (result == null)
            {
                return NotFound(new { message = $"ID {id} ile eşleşen manga bulunamadı." });
            }

            if (result.Puan.HasValue) result.Puan = Math.Round(result.Puan.Value, 1, MidpointRounding.AwayFromZero);

            return Ok(result);
        }

        /// <summary>
        /// Tüm kategorilerin listesi
        /// </summary>
        [HttpGet("kategoriler")]
        public async Task<ActionResult<IEnumerable<string>>> GetCategories()
        {
            var kategoriler = await _context.Kategoriler
                .OrderBy(k => k.KategoriAdi)
                .Select(k => k.KategoriAdi)
                .ToListAsync();

            return Ok(kategoriler);
        }

        /// <summary>
        /// Yeni manga ekleme (Admin yetkisi gerektirir)
        /// </summary>
        [Yetki("manga.yaz")]
        [HttpPost]
        public async Task<ActionResult<MangaDetailDto>> CreateManga([FromBody] CreateMangaRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MangaAdi))
            {
                return BadRequest(new { message = "Manga adı zorunludur." });
            }
            if (request.MangaAdi.Trim().Length > 200)
                return BadRequest(new { message = "Manga adı en fazla 200 karakter olabilir." });
            if (request.Aciklama != null && request.Aciklama.Length > 4000)
                return BadRequest(new { message = "Açıklama en fazla 4000 karakter olabilir." });
            if (request.Yazar != null && request.Yazar.Length > 100)
                return BadRequest(new { message = "Yazar adı en fazla 100 karakter olabilir." });
            if (request.Cizer != null && request.Cizer.Length > 100)
                return BadRequest(new { message = "Çizer adı en fazla 100 karakter olabilir." });
            if (!string.IsNullOrWhiteSpace(request.KapakResmiURL))
            {
                var urlHata = UploadDosyaServisi.GorselUrlHatasi(request.KapakResmiURL, "Kapak görseli adresi");
                if (urlHata != null) return BadRequest(new { message = urlHata });
            }

            var existing = await _context.Mangalar
                .AnyAsync(m => m.MangaAdi.ToLower() == request.MangaAdi.Trim().ToLower());

            if (existing)
            {
                return BadRequest(new { message = "Bu isimde bir manga zaten mevcut." });
            }

            var yeniManga = new Manga
            {
                MangaAdi = request.MangaAdi.Trim(),
                Aciklama = request.Aciklama,
                KapakResmiURL = request.KapakResmiURL,
                Yazar = request.Yazar,
                Cizer = request.Cizer,
                Durum = string.IsNullOrWhiteSpace(request.Durum) ? "DevamEdiyor" : request.Durum,
                EklenmeTarihi = DateTime.UtcNow,
                Puan = 9.0m,          // veritabanı kolonları NOT NULL (varsayılanlar SQL scriptiyle aynı)
                OkunmaSayisi = "0",
                Yayinda = request.Yayinda ?? true,
                OneCikan = request.OneCikan ?? false
            };

            _context.Mangalar.Add(yeniManga);
            await _context.SaveChangesAsync();

            // Kategorileri bağla
            if (request.Kategoriler != null && request.Kategoriler.Any())
            {
                foreach (var katAdi in request.Kategoriler.Distinct())
                {
                    var cleanKat = katAdi.Trim();
                    var kategori = await _context.Kategoriler.FirstOrDefaultAsync(k => k.KategoriAdi.ToLower() == cleanKat.ToLower());
                    if (kategori == null)
                    {
                        kategori = new Kategori { KategoriAdi = cleanKat };
                        _context.Kategoriler.Add(kategori);
                        await _context.SaveChangesAsync();
                    }

                    _context.MangaKategorileri.Add(new MangaKategori
                    {
                        MangaId = yeniManga.Id,
                        KategoriId = kategori.Id
                    });
                }
                await _context.SaveChangesAsync();
            }

            await _denetim.KaydetAsync(User, "manga.olustur", $"Manga #{yeniManga.Id} {yeniManga.MangaAdi}");

            return CreatedAtAction(nameof(GetById), new { id = yeniManga.Id }, new MangaDetailDto
            {
                Id = yeniManga.Id,
                MangaAdi = yeniManga.MangaAdi,
                Aciklama = yeniManga.Aciklama,
                KapakResmiURL = yeniManga.KapakResmiURL,
                Yazar = yeniManga.Yazar,
                Cizer = yeniManga.Cizer,
                Durum = yeniManga.Durum,
                EklenmeTarihi = yeniManga.EklenmeTarihi,
                Kategoriler = request.Kategoriler ?? new List<string>(),
                Yayinda = yeniManga.Yayinda,
                OneCikan = yeniManga.OneCikan,
                Bolumler = new List<BolumSummaryDto>()
            });
        }

        /// <summary>
        /// Manga güncelleme (Admin yetkisi gerektirir)
        /// </summary>
        [Yetki("manga.yaz")]
        [HttpPut("{id}")]
        public async Task<ActionResult<MangaDetailDto>> UpdateManga(int id, [FromBody] UpdateMangaRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MangaAdi))
            {
                return BadRequest(new { message = "Manga adı zorunludur." });
            }
            if (request.MangaAdi.Trim().Length > 200)
                return BadRequest(new { message = "Manga adı en fazla 200 karakter olabilir." });
            if (request.Aciklama != null && request.Aciklama.Length > 4000)
                return BadRequest(new { message = "Açıklama en fazla 4000 karakter olabilir." });
            if (request.Yazar != null && request.Yazar.Length > 100)
                return BadRequest(new { message = "Yazar adı en fazla 100 karakter olabilir." });
            if (request.Cizer != null && request.Cizer.Length > 100)
                return BadRequest(new { message = "Çizer adı en fazla 100 karakter olabilir." });
            if (!string.IsNullOrWhiteSpace(request.KapakResmiURL))
            {
                var urlHata = UploadDosyaServisi.GorselUrlHatasi(request.KapakResmiURL, "Kapak görseli adresi");
                if (urlHata != null) return BadRequest(new { message = urlHata });
            }

            var manga = await _context.Mangalar
                .Include(m => m.MangaKategorileri)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (manga == null)
            {
                return NotFound(new { message = $"ID {id} ile eşleşen manga bulunamadı." });
            }

            var yeniAd = request.MangaAdi.Trim();
            var adCakisiyor = await _context.Mangalar
                .AnyAsync(m => m.Id != id && m.MangaAdi.ToLower() == yeniAd.ToLower());
            if (adCakisiyor)
            {
                return BadRequest(new { message = "Bu isimde bir manga zaten mevcut." });
            }

            var eskiKapak = manga.KapakResmiURL;
            manga.MangaAdi = yeniAd;
            manga.Aciklama = request.Aciklama;
            manga.KapakResmiURL = request.KapakResmiURL;
            if (request.Yayinda.HasValue) manga.Yayinda = request.Yayinda.Value;
            if (request.OneCikan.HasValue) manga.OneCikan = request.OneCikan.Value;
            manga.Yazar = request.Yazar;
            manga.Cizer = request.Cizer;
            if (!string.IsNullOrWhiteSpace(request.Durum))
            {
                manga.Durum = request.Durum.Trim();
            }
            manga.GuncellenmeTarihi = DateTime.UtcNow;

            if (request.Kategoriler != null)
            {
                _context.MangaKategorileri.RemoveRange(manga.MangaKategorileri);

                foreach (var katAdi in request.Kategoriler
                             .Where(k => !string.IsNullOrWhiteSpace(k))
                             .Select(k => k.Trim())
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var kategori = await _context.Kategoriler.FirstOrDefaultAsync(k => k.KategoriAdi.ToLower() == katAdi.ToLower());
                    if (kategori == null)
                    {
                        kategori = new Kategori { KategoriAdi = katAdi };
                        _context.Kategoriler.Add(kategori);
                        await _context.SaveChangesAsync();
                    }

                    _context.MangaKategorileri.Add(new MangaKategori { MangaId = manga.Id, KategoriId = kategori.Id });
                }
            }

            await _context.SaveChangesAsync();

            if (!string.Equals(eskiKapak, manga.KapakResmiURL, StringComparison.Ordinal))
                await UploadDosyaServisi.TemizleAsync(_context, _env, new[] { eskiKapak });

            await _denetim.KaydetAsync(User, "manga.guncelle", $"Manga #{manga.Id} {manga.MangaAdi}");

            return await GetById(id);
        }

        /// <summary>
        /// Manga silme (Admin yetkisi gerektirir). Bölüm, sayfa, yorum, favori ve okuma geçmişi kayıtları da silinir.
        /// </summary>
        [Yetki("manga.sil")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteManga(int id)
        {
            var silinecek = await _context.Mangalar.AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new { m.MangaAdi, m.KapakResmiURL })
                .FirstOrDefaultAsync();
            if (silinecek == null)
            {
                return NotFound(new { message = $"ID {id} ile eşleşen manga bulunamadı." });
            }

            var dosyaUrlleri = await _context.Sayfalar.AsNoTracking()
                .Where(s => s.Bolum.MangaId == id).Select(s => s.ResimURL).ToListAsync();
            dosyaUrlleri.Add(silinecek.KapakResmiURL ?? string.Empty);

            // OkumaGecmisi -> Mangalar/Bolumler FK'leri veritabanında cascade olmayabilir; önce elle temizle.
            var silinenYorumlar = await _context.Yorumlar.AsNoTracking().Where(y => y.Bolum.MangaId == id).Select(y => "yorum:" + y.Id).ToListAsync();
            silinenYorumlar.AddRange(await _context.MangaYorumlari.AsNoTracking().Where(y => y.MangaId == id).Select(y => "mangayorum:" + y.Id).ToListAsync());
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.OkumaGecmisi
                .Where(o => o.MangaId == id || o.SonOkunanBolum.MangaId == id)
                .ExecuteDeleteAsync();
            await _context.Mangalar.Where(m => m.Id == id).ExecuteDeleteAsync();
            await tx.CommitAsync();
            await PuanServisi.YorumPuaniniSilAsync(_context, silinenYorumlar);

            await UploadDosyaServisi.TemizleAsync(_context, _env, dosyaUrlleri);
            await _denetim.KaydetAsync(User, "manga.sil", $"Manga #{id} {silinecek.MangaAdi}");

            return Ok(new { message = "Manga ve bağlı tüm kayıtları silindi." });
        }
    }
}
