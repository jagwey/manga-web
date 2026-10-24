using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
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
    public class ChaptersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IDenetimServisi _denetim;
        private readonly ISiteAyarServisi _ayarlar;
        private readonly YorumServisi _yorumlar;

        public ChaptersController(AppDbContext context, IWebHostEnvironment env, IDenetimServisi denetim, ISiteAyarServisi ayarlar, YorumServisi yorumlar)
        {
            _yorumlar = yorumlar;
            _context = context;
            _env = env;
            _denetim = denetim;
            _ayarlar = ayarlar;
        }

        /// <summary>Bölüm yayında olmayan bir seriye aitse ve çağıran yetkili değilse true (404 dönülmeli).</summary>
        private async Task<bool> GizliMiAsync(int chapterId)
        {
            var yayinda = await _context.Bolumler.AsNoTracking()
                .Where(b => b.Id == chapterId).Select(b => (bool?)b.Manga.Yayinda).FirstOrDefaultAsync();
            if (yayinda != false) return false;
            return !await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context);
        }

        /// <summary>
        /// İlgili bölüme ait sıralı sayfa listesi ve önceki/sonraki bölüm navigasyonu
        /// </summary>
        [HttpGet("{chapterId}/pages")]
        public async Task<ActionResult<ChapterPagesResponse>> GetChapterPages(int chapterId)
        {
            var bolum = await _context.Bolumler
                .Include(b => b.Manga)
                .Include(b => b.Sayfalar)
                .FirstOrDefaultAsync(b => b.Id == chapterId);

            if (bolum == null || (!bolum.Manga.Yayinda && !await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context)))
            {
                return NotFound(new { message = $"Bölüm bulunamadı (ID: {chapterId})." });
            }

            // Önceki ve sonraki bölümü bul
            var digerBolumler = await _context.Bolumler
                .Where(b => b.MangaId == bolum.MangaId)
                .OrderBy(b => b.BolumNo)
                .Select(b => new { b.Id, b.BolumNo })
                .ToListAsync();

            int? oncekiId = digerBolumler.LastOrDefault(b => b.BolumNo < bolum.BolumNo)?.Id;
            int? sonrakiId = digerBolumler.FirstOrDefault(b => b.BolumNo > bolum.BolumNo)?.Id;

            var sayfalar = bolum.Sayfalar
                .OrderBy(s => s.SayfaNo)
                .Select(s => s.ResimURL)
                .ToList();

            return Ok(new ChapterPagesResponse
            {
                MangaId = bolum.MangaId,
                MangaAdi = bolum.Manga.MangaAdi,
                BolumId = bolum.Id,
                BolumNo = bolum.BolumNo,
                BolumAdi = bolum.BolumAdi,
                Sayfalar = sayfalar,
                OncekiBolumId = oncekiId,
                SonrakiBolumId = sonrakiId
            });
        }

        /// <summary>
        /// Bölüm ve sayfaları toplu ekleme (admin.html JSON yükü ile %100 uyumludur)
        /// </summary>
        [Yetki("bolum.yaz")]
        [HttpPost]
        public async Task<ActionResult> CreateChapterWithPages([FromBody] CreateChapterWithPagesRequest request)
        {
            if (request.MangaId == null && string.IsNullOrWhiteSpace(request.MangaAdi))
            {
                return BadRequest(new { message = "Manga ID veya Manga Adı belirtilmelidir." });
            }
            if (!string.IsNullOrWhiteSpace(request.MangaAdi) && request.MangaAdi.Trim().Length > 200)
                return BadRequest(new { message = "Manga adı en fazla 200 karakter olabilir." });
            if (!string.IsNullOrWhiteSpace(request.BolumAdi) && request.BolumAdi.Trim().Length > 200)
                return BadRequest(new { message = "Bölüm adı en fazla 200 karakter olabilir." });
            if (!string.IsNullOrWhiteSpace(request.KapakGorseli))
            {
                var kapakHata = UploadDosyaServisi.GorselUrlHatasi(request.KapakGorseli, "Kapak görseli adresi");
                if (kapakHata != null) return BadRequest(new { message = kapakHata });
            }

            // Sayfaları parse et (hem virgülle ayrılmış string hem dizi kabul edilir)
            var sayfaUrlListesi = new List<string>();
            if (request.Sayfalar != null)
            {
                if (request.Sayfalar is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) sayfaUrlListesi.Add(s.Trim());
                        }
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var s = jsonElement.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            var parts = s.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            sayfaUrlListesi.AddRange(parts.Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)));
                        }
                    }
                }
                else if (request.Sayfalar is string str)
                {
                    var parts = str.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    sayfaUrlListesi.AddRange(parts.Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)));
                }
            }

            if (sayfaUrlListesi.Count > 200)
                return BadRequest(new { message = "Tek seferde en fazla 200 sayfa eklenebilir." });
            foreach (var u in sayfaUrlListesi)
            {
                var sayfaHata = UploadDosyaServisi.GorselUrlHatasi(u, "Sayfa görseli adresi");
                if (sayfaHata != null) return BadRequest(new { message = sayfaHata });
            }

            Manga? manga = null;
            if (request.MangaId.HasValue)
            {
                manga = await _context.Mangalar.FindAsync(request.MangaId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.MangaAdi))
            {
                manga = await _context.Mangalar.FirstOrDefaultAsync(m => m.MangaAdi.ToLower() == request.MangaAdi.Trim().ToLower());
                // Manga henüz yoksa admin.html kolaylığı için otomatik oluştur
                if (manga == null)
                {
                    manga = new Manga
                    {
                        MangaAdi = request.MangaAdi.Trim(),
                        KapakResmiURL = request.KapakGorseli,
                        Aciklama = "Admin paneli üzerinden otomatik eklendi.",
                        Durum = "DevamEdiyor",
                        EklenmeTarihi = DateTime.UtcNow,
                        Puan = 9.0m,
                        OkunmaSayisi = "0"
                    };
                    _context.Mangalar.Add(manga);
                    await _context.SaveChangesAsync();
                    await _denetim.KaydetAsync(User, "manga.olustur", $"Manga #{manga.Id} {manga.MangaAdi}", "Bölüm eklenirken otomatik oluşturuldu");
                }
            }

            if (manga == null)
            {
                return NotFound(new { message = "Belirtilen manga bulunamadı." });
            }

            // Bölüm numarasını normalize et (arama ve oluşturma aynı değeri kullansın)
            var bolumNo = request.BolumNo <= 0 ? 1 : request.BolumNo;

            // Bölüm var mı kontrol et
            var bolum = await _context.Bolumler
                .Include(b => b.Sayfalar)
                .FirstOrDefaultAsync(b => b.MangaId == manga.Id && b.BolumNo == bolumNo);

            if (bolum == null)
            {
                bolum = new Bolum
                {
                    MangaId = manga.Id,
                    BolumNo = bolumNo,
                    BolumAdi = string.IsNullOrWhiteSpace(request.BolumAdi) ? $"Bölüm {bolumNo}" : request.BolumAdi.Trim(),
                    YayinTarihi = DateTime.UtcNow
                };
                _context.Bolumler.Add(bolum);
                await _context.SaveChangesAsync();
            }

            // Sayfaları ekle
            // Bölüm zaten varsa mevcut sayfaların devamından numaralandır (UNIQUE (BolumId, SayfaNo) ihlali olmasın)
            int sayfaNo = (bolum.Sayfalar.Count == 0 ? 0 : bolum.Sayfalar.Max(p => p.SayfaNo)) + 1;
            foreach (var url in sayfaUrlListesi)
            {
                _context.Sayfalar.Add(new Sayfa
                {
                    BolumId = bolum.Id,
                    SayfaNo = sayfaNo++,
                    ResimURL = url
                });
            }

            manga.GuncellenmeTarihi = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "bolum.olustur", $"Bölüm #{bolum.Id} ({manga.MangaAdi} {bolum.BolumNo})",
                $"{sayfaUrlListesi.Count} sayfa eklendi");

            return Ok(new
            {
                message = "Bölüm ve sayfalar başarıyla kaydedildi.",
                mangaId = manga.Id,
                bolumId = bolum.Id,
                eklenenSayfaSayisi = sayfaUrlListesi.Count
            });
        }

        /// <summary>
        /// Bölüme ait yorumların listesi
        /// </summary>
        [HttpGet("{chapterId}/comments")]
        public async Task<ActionResult<IEnumerable<YorumDto>>> GetComments(int chapterId,
            [FromQuery] string? sirala = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (page > 100000) page = 100000;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            if (await GizliMiAsync(chapterId))
            {
                return NotFound(new { message = "Bölüm bulunamadı." });
            }

            int? uid = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var u) ? u : null;
            var (liste, toplam) = await _yorumlar.ListeAsync(false, chapterId, uid, sirala, page, pageSize);
            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(liste);
        }

        /// <summary>
        /// Bölüme yeni yorum ekleme (Oturum açmış kullanıcı gerektirir)
        /// </summary>
        [Authorize]
        [HttpPost("{chapterId}/comments")]
        public async Task<ActionResult<YorumDto>> AddComment(int chapterId, [FromBody] CreateYorumRequest request)
        {
            var (hata, temizMetin) = YorumServisi.MetinHazirla(request?.Metin);
            if (hata != null) return BadRequest(new { message = hata });

            if (!await _ayarlar.AcikMiAsync("yorumAcik", true))
            {
                return StatusCode(403, new { message = "Yorumlar şu an kapalı." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var bolumExists = await _context.Bolumler.AnyAsync(b => b.Id == chapterId);
            if (!bolumExists || await GizliMiAsync(chapterId))
            {
                return NotFound(new { message = "Yorum yapılacak bölüm bulunamadı." });
            }

            var (kokId, yanitHata) = await _yorumlar.YanitHedefiAsync(false, chapterId, request!.YanitId);
            if (yanitHata != null) return BadRequest(new { message = yanitHata });

            if (await YorumKurallari.SpamMiAsync(_context, userId, HttpContext.RequestAborted))
            {
                return StatusCode(429, new { message = YorumKurallari.SpamMesaji });
            }

            var yorum = new Yorum
            {
                KullaniciId = userId,
                BolumId = chapterId,
                Metin = temizMetin,
                Tarih = DateTime.UtcNow,
                BegeniSayisi = 0,
                Spoiler = request.Spoiler == true,
                YanitId = kokId
            };

            _context.Yorumlar.Add(yorum);
            await _context.SaveChangesAsync();
            var kazanim = await PuanServisi.YorumPuaniAsync(_context, userId, false, yorum.Id, request.Metin, yorum.Metin);

            var dto = await _yorumlar.TekAsync(false, chapterId, yorum.Id, userId);
            if (dto != null) dto.PuanKazanildi = kazanim.Kazanilan;
            return Ok(dto);
        }

        /// <summary>
        /// Bölüm numarası ve adını düzenleme
        /// </summary>
        [Yetki("bolum.duzenle")]
        [HttpPut("{chapterId:int}")]
        public async Task<ActionResult> UpdateChapter(int chapterId, [FromBody] BolumGuncelleRequest request)
        {
            var bolum = await _context.Bolumler.Include(b => b.Manga).FirstOrDefaultAsync(b => b.Id == chapterId);
            if (bolum == null)
            {
                return NotFound(new { message = $"Bölüm bulunamadı (ID: {chapterId})." });
            }
            if (request == null || (request.BolumNo == null && request.BolumAdi == null))
            {
                return BadRequest(new { message = "Güncellenecek alan belirtilmedi." });
            }

            var eskiNo = bolum.BolumNo;
            var eskiAd = bolum.BolumAdi;
            if (request.BolumNo.HasValue)
            {
                var no = Math.Round(request.BolumNo.Value, 2);
                if (no <= 0 || no >= 10000)
                    return BadRequest(new { message = "Bölüm numarası 0 dan büyük ve 10000 den küçük olmalıdır." });
                if (await _context.Bolumler.AnyAsync(b => b.MangaId == bolum.MangaId && b.Id != chapterId && b.BolumNo == no))
                    return BadRequest(new { message = $"Bu seride {no} numaralı bir bölüm zaten var." });
                bolum.BolumNo = no;
            }
            if (request.BolumAdi != null)
            {
                var ad = request.BolumAdi.Trim();
                if (ad.Length > 200)
                    return BadRequest(new { message = "Bölüm adı en fazla 200 karakter olabilir." });
                bolum.BolumAdi = ad.Length == 0 ? $"Bölüm {bolum.BolumNo}" : ad;
            }
            bolum.Manga.GuncellenmeTarihi = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { message = "Bu bölüm numarası bu seride zaten kullanılıyor." });
            }

            await _denetim.KaydetAsync(User, "bolum.guncelle", $"Bölüm #{chapterId} ({bolum.Manga.MangaAdi})",
                $"No: {eskiNo} -> {bolum.BolumNo}; Ad: {eskiAd} -> {bolum.BolumAdi}");

            return Ok(new { id = bolum.Id, mangaId = bolum.MangaId, bolumNo = bolum.BolumNo, bolumAdi = bolum.BolumAdi });
        }

        /// <summary>
        /// Bölüm silme (Admin yetkisi gerektirir). Sayfalar, yorumlar ve okuma geçmişi kayıtları da silinir.
        /// </summary>
        [Yetki("bolum.sil")]
        [HttpDelete("{chapterId}")]
        public async Task<ActionResult> DeleteChapter(int chapterId)
        {
            var bolum = await _context.Bolumler
                .AsNoTracking()
                .Where(b => b.Id == chapterId)
                .Select(b => new { b.Id, b.MangaId, MangaAdi = b.Manga.MangaAdi, b.BolumNo })
                .FirstOrDefaultAsync();

            if (bolum == null)
            {
                return NotFound(new { message = $"Bölüm bulunamadı (ID: {chapterId})." });
            }

            var dosyaUrlleri = await _context.Sayfalar.AsNoTracking()
                .Where(s => s.BolumId == chapterId).Select(s => s.ResimURL).ToListAsync();

            // OkumaGecmisi -> Bolumler FK'si veritabanında cascade olmayabilir; önce elle temizle.
            await using var tx = await _context.Database.BeginTransactionAsync();
            var silinenYorumlar = await _context.Yorumlar.AsNoTracking().Where(y => y.BolumId == chapterId).Select(y => "yorum:" + y.Id).ToListAsync();
            await _context.OkumaGecmisi.Where(o => o.SonOkunanBolumId == chapterId).ExecuteDeleteAsync();
            await _context.Bolumler.Where(b => b.Id == chapterId).ExecuteDeleteAsync();
            await _context.Mangalar.Where(m => m.Id == bolum.MangaId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.GuncellenmeTarihi, DateTime.UtcNow));
            await tx.CommitAsync();
            await PuanServisi.YorumPuaniniSilAsync(_context, silinenYorumlar);

            await UploadDosyaServisi.TemizleAsync(_context, _env, dosyaUrlleri);
            await _denetim.KaydetAsync(User, "bolum.sil", $"Bölüm #{chapterId} ({bolum.MangaAdi} {bolum.BolumNo})");

            return Ok(new { message = "Bölüm ve bağlı sayfa/yorum kayıtları silindi." });
        }
    }
}
