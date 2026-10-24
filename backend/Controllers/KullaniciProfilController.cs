using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api/kullanicilar")]
    public class KullaniciProfilController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TercihServisi _tercihler;

        public KullaniciProfilController(AppDbContext db, TercihServisi tercihler)
        {
            _db = db;
            _tercihler = tercihler;
        }

        [HttpGet("{kullaniciAdi}/profil")]
        public async Task<IActionResult> ProfilGetir(string kullaniciAdi)
        {
            if (string.IsNullOrWhiteSpace(kullaniciAdi))
                return NotFound(new { message = "Kullanıcı bulunamadı." });

            var aranan = kullaniciAdi.Trim();
            var k = await _db.Kullanicilar.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Aktif && x.KullaniciAdi == aranan);

            if (k == null)
                return NotFound(new { message = "Kullanıcı bulunamadı." });

            var toplamPuan = await _tercihler.ToplamPuanAsync(k.Id);
            var seviye = PuanServisi.Seviye(toplamPuan);
            var rutbe = PuanServisi.RutbeSeviyeden(seviye);
            var adStili = await _tercihler.TekAsync(k.Id);

            var kutuphane = await _db.Favoriler.AsNoTracking()
                .Where(f => f.KullaniciId == k.Id && f.Manga.Yayinda)
                .OrderByDescending(f => f.EklenmeTarihi)
                .Take(24)
                .Select(f => new
                {
                    id = f.Manga.Id,
                    mangaAdi = f.Manga.MangaAdi,
                    kapakResmiURL = f.Manga.KapakResmiURL,
                    durum = f.Manga.Durum,
                    puan = f.Manga.Puan
                })
                .ToListAsync();

            var bolumYorumlari = await _db.Yorumlar.AsNoTracking()
                .Where(y => y.KullaniciId == k.Id && !y.Spoiler && y.Bolum.Manga.Yayinda)
                .OrderByDescending(y => y.Tarih)
                .Take(10)
                .Select(y => new
                {
                    id = y.Id,
                    tur = "bolum",
                    hedefBaslik = y.Bolum.Manga.MangaAdi + " · Bölüm " + y.Bolum.BolumNo,
                    baglanti = "read.html?chapter=" + y.BolumId,
                    metin = y.Metin,
                    tarih = y.Tarih
                })
                .ToListAsync();

            var mangaYorumlari = await _db.MangaYorumlari.AsNoTracking()
                .Where(y => y.KullaniciId == k.Id && !y.Spoiler && y.Manga.Yayinda)
                .OrderByDescending(y => y.Tarih)
                .Take(10)
                .Select(y => new
                {
                    id = y.Id,
                    tur = "manga",
                    hedefBaslik = y.Manga.MangaAdi,
                    baglanti = "#/manga/" + y.MangaId,
                    metin = y.Metin,
                    tarih = y.Tarih
                })
                .ToListAsync();

            var sonYorumlar = bolumYorumlari.Concat(mangaYorumlari)
                .OrderByDescending(y => y.tarih)
                .Take(10)
                .ToList();

            return Ok(new
            {
                id = k.Id,
                kullaniciAdi = k.KullaniciAdi,
                rol = k.Rol,
                profilResmiURL = k.ProfilResmiURL,
                kayitTarihi = k.KayitTarihi,
                toplamPuan,
                seviye,
                rutbe,
                adStili,
                kutuphane,
                sonYorumlar
            });
        }
    }
}
