using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    /// <summary>Yorum v2: düzenle, sil, beğen, şikayet et; şikayet yönetimi.</summary>
    [ApiController]
    [Route("api")]
    public class YorumEtkilesimController : ControllerBase
    {
        private const string HizMesaji = "Çok hızlı işlem yapıyorsunuz. Lütfen biraz bekleyip tekrar deneyin.";
        private const int DakikaSiniri = 30;
        private const int GunlukSikayet = 10;

        private readonly AppDbContext _context;
        private readonly YorumServisi _yorumlar;
        private readonly IslemSinirlayici _sinir;
        private readonly IDenetimServisi _denetim;

        public YorumEtkilesimController(AppDbContext context, YorumServisi yorumlar, IslemSinirlayici sinir, IDenetimServisi denetim)
        {
            _context = context;
            _yorumlar = yorumlar;
            _sinir = sinir;
            _denetim = denetim;
        }

        private int? KullaniciId()
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private bool HizliMi(int uid) => !_sinir.Dene("yorumislem:" + uid, DakikaSiniri, TimeSpan.FromMinutes(1));

        private static bool TurCoz(string? tur, out bool manga)
        {
            manga = false;
            if (string.Equals(tur, "bolum", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(tur, "manga", StringComparison.OrdinalIgnoreCase)) { manga = true; return true; }
            return false;
        }

        // ---------- Düzenle ----------

        [Authorize]
        [HttpPut("chapters/comments/{id:int}")]
        public Task<IActionResult> BolumYorumDuzenle(int id, [FromBody] YorumGuncelleRequest? istek) => DuzenleAsync(false, id, istek);

        [Authorize]
        [HttpPut("mangas/yorumlar/{id:int}")]
        public Task<IActionResult> MangaYorumDuzenle(int id, [FromBody] YorumGuncelleRequest? istek) => DuzenleAsync(true, id, istek);

        private async Task<IActionResult> DuzenleAsync(bool manga, int id, YorumGuncelleRequest? istek)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();
            if (istek == null) return BadRequest(new { message = "Geçersiz istek." });
            var (hata, kodlu) = YorumServisi.MetinHazirla(istek.Metin);
            if (hata != null) return BadRequest(new { message = hata });

            var bilgi = await _yorumlar.BilgiAsync(manga, id);
            if (bilgi == null) return NotFound(new { message = "Yorum bulunamadı." });
            if (bilgi.KullaniciId != uid.Value) return StatusCode(403, new { message = "Yalnızca kendi yorumunuzu düzenleyebilirsiniz." });
            if (HizliMi(uid.Value)) return StatusCode(429, new { message = HizMesaji });

            var simdi = DateTime.UtcNow;
            if (manga)
            {
                var y = await _context.MangaYorumlari.FirstAsync(x => x.Id == id);
                if (y.Metin != kodlu || (istek.Spoiler.HasValue && y.Spoiler != istek.Spoiler.Value))
                {
                    y.Metin = kodlu; if (istek.Spoiler.HasValue) y.Spoiler = istek.Spoiler.Value; y.Duzenlendi = simdi;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var y = await _context.Yorumlar.FirstAsync(x => x.Id == id);
                if (y.Metin != kodlu || (istek.Spoiler.HasValue && y.Spoiler != istek.Spoiler.Value))
                {
                    y.Metin = kodlu; if (istek.Spoiler.HasValue) y.Spoiler = istek.Spoiler.Value; y.Duzenlendi = simdi;
                    await _context.SaveChangesAsync();
                }
            }
            return Ok(await _yorumlar.TekAsync(manga, bilgi.UstId, id, uid.Value));
        }

        // ---------- Sil ----------

        [Authorize]
        [HttpDelete("chapters/comments/{id:int}")]
        public Task<IActionResult> BolumYorumSil(int id) => SilAsync(false, id);

        [Authorize]
        [HttpDelete("mangas/yorumlar/{id:int}")]
        public Task<IActionResult> MangaYorumSil(int id) => SilAsync(true, id);

        private async Task<IActionResult> SilAsync(bool manga, int id)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();
            var bilgi = await _yorumlar.BilgiAsync(manga, id);
            if (bilgi == null) return NotFound(new { message = "Yorum bulunamadı." });

            var sahibi = bilgi.KullaniciId == uid.Value;
            if (!sahibi)
            {
                var k = await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid.Value);
                if (k == null || !Izinler.Var(k, "yorum.sil"))
                    return StatusCode(403, new { message = "Bu yorumu silme yetkiniz yok." });
            }
            if (HizliMi(uid.Value)) return StatusCode(429, new { message = HizMesaji });

            var silinen = await _yorumlar.SilAsync(manga, id);
            if (!sahibi)
            {
                var metin = bilgi.Metin.Length > 200 ? bilgi.Metin[..200] : bilgi.Metin;
                await _denetim.KaydetAsync(User, "yorum.sil",
                    $"{(manga ? "Seri yorumu" : "Yorum")} #{id} (kullanıcı #{bilgi.KullaniciId})", metin);
            }
            return Ok(new { message = "Yorum silindi.", silinen = silinen.Count });
        }

        // ---------- Beğeni ----------

        [Authorize]
        [HttpPost("yorumlar/{tur}/{id:int}/begeni")]
        public async Task<IActionResult> BegeniDegistir(string tur, int id)
        {
            if (!TurCoz(tur, out var manga)) return BadRequest(new { message = "tur 'bolum' ya da 'manga' olmalıdır." });
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();

            var bilgi = await _yorumlar.BilgiAsync(manga, id);
            if (bilgi == null || (!bilgi.Yayinda && !await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context)))
                return NotFound(new { message = "Yorum bulunamadı." });
            if (bilgi.KullaniciId == uid.Value) return BadRequest(new { message = "Kendi yorumunuzu beğenemezsiniz." });
            if (HizliMi(uid.Value)) return StatusCode(429, new { message = HizMesaji });

            var t = YorumServisi.Tur(manga);
            bool begendi;
            var mevcut = await _context.YorumBegenileri.FirstOrDefaultAsync(b => b.Tur == t && b.YorumId == id && b.KullaniciId == uid.Value);
            if (mevcut != null)
            {
                _context.YorumBegenileri.Remove(mevcut);
                await _context.SaveChangesAsync();
                begendi = false;
            }
            else
            {
                _context.YorumBegenileri.Add(new YorumBegeni { Tur = t, YorumId = id, KullaniciId = uid.Value, Tarih = DateTime.UtcNow });
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateException) { _context.ChangeTracker.Clear(); } // eşzamanlı çift istek: zaten beğenilmiş
                begendi = true;
            }

            var sayi = await _context.YorumBegenileri.CountAsync(b => b.Tur == t && b.YorumId == id);
            if (!manga) // bölüm yorumundaki BegeniSayisi kolonunu senkron tut
                await _context.Yorumlar.Where(y => y.Id == id).ExecuteUpdateAsync(s => s.SetProperty(y => y.BegeniSayisi, sayi));
            return Ok(new { begendi, begeniSayisi = sayi });
        }

        // ---------- Şikayet ----------

        [Authorize]
        [HttpPost("yorumlar/{tur}/{id:int}/sikayet")]
        public async Task<IActionResult> Sikayet(string tur, int id, [FromBody] YorumSikayetRequest? istek)
        {
            if (!TurCoz(tur, out var manga)) return BadRequest(new { message = "tur 'bolum' ya da 'manga' olmalıdır." });
            var uid = KullaniciId();
            if (uid == null) return Unauthorized();

            var neden = (istek?.Neden ?? string.Empty).Trim();
            if (neden.Length == 0) return BadRequest(new { message = "Şikayet nedeni boş bırakılamaz." });
            if (neden.Length > 150) return BadRequest(new { message = "Şikayet nedeni en fazla 150 karakter olabilir." });
            var kodlu = YorumKurallari.Kodla(neden);
            if (kodlu.Length > 200) return BadRequest(new { message = "Şikayet nedeni çok fazla özel karakter içeriyor." });

            var bilgi = await _yorumlar.BilgiAsync(manga, id);
            if (bilgi == null || (!bilgi.Yayinda && !await IcerikErisimi.GizliGorebilirMiAsync(HttpContext, _context)))
                return NotFound(new { message = "Yorum bulunamadı." });
            if (bilgi.KullaniciId == uid.Value) return BadRequest(new { message = "Kendi yorumunuzu şikayet edemezsiniz." });
            if (HizliMi(uid.Value)) return StatusCode(429, new { message = HizMesaji });

            var t = YorumServisi.Tur(manga);
            if (await _context.YorumSikayetleri.AnyAsync(s => s.Tur == t && s.YorumId == id && s.SikayetEdenId == uid.Value))
                return Conflict(new { message = "Bu yorumu zaten şikayet ettiniz." });
            var sinir = DateTime.UtcNow.AddHours(-24);
            if (await _context.YorumSikayetleri.CountAsync(s => s.SikayetEdenId == uid.Value && s.Tarih > sinir) >= GunlukSikayet)
                return StatusCode(429, new { message = $"Günde en fazla {GunlukSikayet} şikayet gönderebilirsiniz." });

            _context.YorumSikayetleri.Add(new YorumSikayeti { Tur = t, YorumId = id, SikayetEdenId = uid.Value, Neden = kodlu, Tarih = DateTime.UtcNow, Durum = "acik" });
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException) { return Conflict(new { message = "Bu yorumu zaten şikayet ettiniz." }); }
            return Ok(new { message = "Şikayetiniz alındı. Teşekkürler." });
        }

        // ---------- Yönetim: şikayetler ----------

        [Yetki("yorum.sil")]
        [HttpGet("admin/sikayetler")]
        public async Task<IActionResult> Sikayetler([FromQuery] string? durum = "acik", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            durum = (durum ?? "acik").Trim().ToLowerInvariant();
            if (durum != "acik" && durum != "kapali" && durum != "tumu")
                return BadRequest(new { message = "durum 'acik', 'kapali' ya da 'tumu' olmalıdır." });
            if (page < 1) page = 1;
            if (page > 100000) page = 100000;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var q = _context.YorumSikayetleri.AsNoTracking().AsQueryable();
            if (durum != "tumu") q = q.Where(s => s.Durum == durum);

            var gruplar = q.GroupBy(s => new { s.Tur, s.YorumId })
                .Select(g => new { g.Key.Tur, g.Key.YorumId, Sayi = g.Count(), Son = g.Max(x => x.Tarih) });
            var toplam = await gruplar.CountAsync();
            var sayfa = await gruplar.OrderByDescending(g => g.Sayi).ThenByDescending(g => g.Son).ThenBy(g => g.YorumId)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var sonuc = new List<object>();
            if (sayfa.Count > 0)
            {
                var bolumIdler = sayfa.Where(g => g.Tur == "bolum").Select(g => g.YorumId).ToList();
                var mangaIdler = sayfa.Where(g => g.Tur == "manga").Select(g => g.YorumId).ToList();
                var bolumYorumlari = await _context.Yorumlar.AsNoTracking().Where(y => bolumIdler.Contains(y.Id))
                    .Select(y => new { y.Id, y.Metin, y.Tarih, y.KullaniciId, Yazar = y.Kullanici.KullaniciAdi, MangaId = y.Bolum.MangaId, MangaAdi = y.Bolum.Manga.MangaAdi, BolumId = (int?)y.BolumId, BolumNo = (decimal?)y.Bolum.BolumNo })
                    .ToDictionaryAsync(y => y.Id);
                var mangaYorumlari = await _context.MangaYorumlari.AsNoTracking().Where(y => mangaIdler.Contains(y.Id))
                    .Select(y => new { y.Id, y.Metin, y.Tarih, y.KullaniciId, Yazar = y.Kullanici.KullaniciAdi, MangaId = y.MangaId, MangaAdi = y.Manga.MangaAdi })
                    .ToDictionaryAsync(y => y.Id);
                var nedenler = await q.Where(s => (s.Tur == "bolum" && bolumIdler.Contains(s.YorumId)) || (s.Tur == "manga" && mangaIdler.Contains(s.YorumId)))
                    .OrderByDescending(s => s.Tarih).Select(s => new { s.Tur, s.YorumId, s.Neden }).ToListAsync();

                foreach (var g in sayfa)
                {
                    var n = nedenler.Where(x => x.Tur == g.Tur && x.YorumId == g.YorumId).Select(x => x.Neden).Take(20).ToList();
                    if (g.Tur == "bolum" && bolumYorumlari.TryGetValue(g.YorumId, out var by))
                        sonuc.Add(new { tur = "bolum", yorumId = g.YorumId, metin = by.Metin, tarih = by.Tarih, yazarId = by.KullaniciId, yazarAdi = by.Yazar, mangaId = by.MangaId, mangaAdi = by.MangaAdi, bolumId = by.BolumId, bolumNo = by.BolumNo, sikayetSayisi = g.Sayi, sonSikayet = g.Son, nedenler = n });
                    else if (g.Tur == "manga" && mangaYorumlari.TryGetValue(g.YorumId, out var my))
                        sonuc.Add(new { tur = "manga", yorumId = g.YorumId, metin = my.Metin, tarih = my.Tarih, yazarId = my.KullaniciId, yazarAdi = my.Yazar, mangaId = my.MangaId, mangaAdi = my.MangaAdi, bolumId = (int?)null, bolumNo = (decimal?)null, sikayetSayisi = g.Sayi, sonSikayet = g.Son, nedenler = n });
                }
            }
            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(sonuc);
        }

        [Yetki("yorum.sil")]
        [HttpPost("admin/sikayetler/{tur}/{yorumId:int}/kapat")]
        public async Task<IActionResult> SikayetKapat(string tur, int yorumId)
        {
            if (!TurCoz(tur, out var manga)) return BadRequest(new { message = "tur 'bolum' ya da 'manga' olmalıdır." });
            var t = YorumServisi.Tur(manga);
            var adet = await _context.YorumSikayetleri.Where(s => s.Tur == t && s.YorumId == yorumId && s.Durum == "acik")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Durum, "kapali"));
            if (adet == 0) return NotFound(new { message = "Bu yorum için açık şikayet yok." });
            await _denetim.KaydetAsync(User, "yorum.sikayetKapat", $"{(manga ? "Seri yorumu" : "Yorum")} #{yorumId}", $"{adet} şikayet kapatıldı");
            return Ok(new { message = "Şikayetler kapatıldı.", kapatilan = adet });
        }
    }
}
