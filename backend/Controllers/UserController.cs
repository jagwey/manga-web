using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        /// <summary>
        /// Kullanıcının okuma listesi / favorileri
        /// </summary>
        [HttpGet("library")]
        public async Task<ActionResult<IEnumerable<MangaListDto>>> GetLibrary()
        {
            var userId = GetUserId();

            var mangas = await _context.Favoriler
                .Where(f => f.KullaniciId == userId)
                .Include(f => f.Manga)
                    .ThenInclude(m => m.MangaKategorileri)
                        .ThenInclude(mk => mk.Kategori)
                .Include(f => f.Manga.Bolumler)
                .Select(f => new MangaListDto
                {
                    Id = f.Manga.Id,
                    MangaAdi = f.Manga.MangaAdi,
                    Aciklama = f.Manga.Aciklama,
                    KapakResmiURL = f.Manga.KapakResmiURL,
                    Yazar = f.Manga.Yazar,
                    Cizer = f.Manga.Cizer,
                    Durum = f.Manga.Durum,
                    EklenmeTarihi = f.EklenmeTarihi,
                    Kategoriler = f.Manga.MangaKategorileri.Select(mk => mk.Kategori.KategoriAdi).ToList(),
                    BolumSayisi = f.Manga.Bolumler.Count
                })
                .ToListAsync();

            return Ok(mangas);
        }

        /// <summary>
        /// Okunacaklara ekleme / çıkarma (Toggle)
        /// </summary>
        [HttpPost("library/toggle")]
        public async Task<ActionResult> ToggleLibrary([FromBody] ToggleLibraryRequest request)
        {
            var userId = GetUserId();

            var mangaExists = await _context.Mangalar.AnyAsync(m => m.Id == request.MangaId);
            if (!mangaExists)
            {
                return NotFound(new { message = "Manga bulunamadı." });
            }

            var existing = await _context.Favoriler
                .FirstOrDefaultAsync(f => f.KullaniciId == userId && f.MangaId == request.MangaId);

            if (existing != null)
            {
                _context.Favoriler.Remove(existing);
                await _context.SaveChangesAsync();
                return Ok(new { isSaved = false, message = "Manga okuma listenizden çıkarıldı.", puanKazanildi = 0 });
            }
            else
            {
                _context.Favoriler.Add(new Favori
                {
                    KullaniciId = userId,
                    MangaId = request.MangaId,
                    EklenmeTarihi = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                var puan = await PuanServisi.KazandirAsync(_context, userId, PuanServisi.KutuphaneEkle, "kutuphane:" + request.MangaId);
                return Ok(new { isSaved = true, message = "Manga okuma listenize eklendi.", puanKazanildi = puan.Kazanilan });
            }
        }

        /// <summary>
        /// Belirli bir manganın kullanıcının okuma listesinde olup olmadığını kontrol etme
        /// </summary>
        [HttpGet("library/check/{mangaId}")]
        public async Task<ActionResult> CheckLibrary(int mangaId)
        {
            var userId = GetUserId();
            var exists = await _context.Favoriler
                .AnyAsync(f => f.KullaniciId == userId && f.MangaId == mangaId);

            return Ok(new { isSaved = exists });
        }

        /// <summary>
        /// Kullanıcının bir mangadaki son okuma ilerlemesini getirme
        /// </summary>
        [HttpGet("progress/{mangaId}")]
        public async Task<ActionResult<ProgressDto>> GetProgress(int mangaId)
        {
            var userId = GetUserId();

            var progress = await _context.OkumaGecmisi
                .Include(p => p.Manga)
                .Include(p => p.SonOkunanBolum)
                .FirstOrDefaultAsync(p => p.KullaniciId == userId && p.MangaId == mangaId);

            if (progress == null)
            {
                return Ok(new { hasProgress = false });
            }

            return Ok(new
            {
                hasProgress = true,
                mangaId = progress.MangaId,
                mangaAdi = progress.Manga.MangaAdi,
                sonOkunanBolumId = progress.SonOkunanBolumId,
                sonOkunanBolumNo = progress.SonOkunanBolum.BolumNo,
                sonOkunanBolumAdi = progress.SonOkunanBolum.BolumAdi,
                sonSayfaNo = progress.SonSayfaNo,
                guncellenmeTarihi = progress.GuncellenmeTarihi
            });
        }

        /// <summary>
        /// Kullanıcının okuma ilerlemesini kaydetme (Kaldığı bölüm ve sayfa)
        /// </summary>
        [HttpPost("progress")]
        public async Task<ActionResult> SaveProgress([FromBody] SaveProgressRequest request)
        {
            var userId = GetUserId();

            var bolumExists = await _context.Bolumler
                .AnyAsync(b => b.Id == request.BolumId && b.MangaId == request.MangaId);

            if (!bolumExists)
            {
                return BadRequest(new { message = "Belirtilen bölüm bu mangaya ait değil veya bulunamadı." });
            }

            var progress = await _context.OkumaGecmisi
                .FirstOrDefaultAsync(p => p.KullaniciId == userId && p.MangaId == request.MangaId);

            if (progress == null)
            {
                progress = new OkumaGecmisi
                {
                    KullaniciId = userId,
                    MangaId = request.MangaId,
                    SonOkunanBolumId = request.BolumId,
                    SonSayfaNo = request.SayfaNo <= 0 ? 1 : request.SayfaNo,
                    GuncellenmeTarihi = DateTime.UtcNow
                };
                _context.OkumaGecmisi.Add(progress);
            }
            else
            {
                progress.SonOkunanBolumId = request.BolumId;
                progress.SonSayfaNo = request.SayfaNo <= 0 ? 1 : request.SayfaNo;
                progress.GuncellenmeTarihi = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var kazanim = await PuanServisi.OkumaIlerlemesiAsync(_context, userId, request.BolumId, progress.SonSayfaNo);
            return Ok(new { message = "Okuma ilerlemeniz kaydedildi.", sonSayfaNo = progress.SonSayfaNo, puanKazanildi = kazanim.Kazanilan });
        }
    }
}
