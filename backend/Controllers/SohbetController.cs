using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Hubs;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    public class SohbetSusturRequest
    {
        public int KullaniciId { get; set; }
        public int Dakika { get; set; }
        public string? Neden { get; set; }
    }

    [ApiController]
    [Route("api")]
    public class SohbetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<SohbetHub> _hub;
        private readonly IDenetimServisi _denetim;
        private readonly ISiteAyarServisi _ayarlar;
        private readonly TercihServisi _tercihler;

        public SohbetController(AppDbContext context, IHubContext<SohbetHub> hub, IDenetimServisi denetim, ISiteAyarServisi ayarlar, TercihServisi tercihler)
        {
            _context = context;
            _hub = hub;
            _denetim = denetim;
            _ayarlar = ayarlar;
            _tercihler = tercihler;
        }

        [HttpGet("sohbet/durum")]
        public async Task<IActionResult> Durum()
            => Ok(new { cevrimici = SohbetHub.Cevrimici, acik = await _ayarlar.AcikMiAsync("sohbetAcik", true) });

        /// <summary>Geçmiş mesajlar (anonim). onceki: bu kimlikten eski olanlar; en yeni "limit" mesaj ESKİDEN YENİYE sırayla döner.</summary>
        [HttpGet("sohbet/mesajlar")]
        public async Task<IActionResult> Mesajlar([FromQuery] int? onceki = null, [FromQuery] int limit = 50)
        {
            if (limit < 1) limit = 50;
            if (limit > 100) limit = 100;
            var q = _context.SohbetMesajlari.AsNoTracking().Where(m => !m.Silindi);
            if (onceki.HasValue) q = q.Where(m => m.Id < onceki.Value);
            var ham = await q.OrderByDescending(m => m.Id).Take(limit)
                .Select(m => new { m.Id, m.KullaniciId, Ad = m.Kullanici.KullaniciAdi, m.Kullanici.ProfilResmiURL, m.Metin, m.StickerId, m.Tarih })
                .ToListAsync();
            ham.Reverse();

            var idler = ham.Select(m => m.KullaniciId).Distinct().ToList();
            var toplamlar = idler.Count == 0 ? new Dictionary<int, int>()
                : await _context.PuanKayitlari.AsNoTracking().Where(p => p.Puan > 0 && idler.Contains(p.KullaniciId))
                    .GroupBy(p => p.KullaniciId).Select(g => new { g.Key, T = g.Sum(x => x.Puan) }).ToDictionaryAsync(x => x.Key, x => x.T);
            var stiller = await _tercihler.CozAsync(idler);

            return Ok(ham.Select(m =>
            {
                var sev = PuanServisi.Seviye(toplamlar.GetValueOrDefault(m.KullaniciId));
                return new SohbetMesajDto
                {
                    Id = m.Id, KullaniciId = m.KullaniciId, KullaniciAdi = m.Ad, ProfilResmiURL = m.ProfilResmiURL,
                    Rutbe = PuanServisi.RutbeSeviyeden(sev), Seviye = sev, Metin = m.Metin, StickerId = m.StickerId,
                    Tarih = SohbetMetni.Utc(m.Tarih), AdStili = stiller.GetValueOrDefault(m.KullaniciId)
                };
            }));
        }

        // ---------- Yönetim ----------

        [Yetki("sohbet.yonet")]
        [HttpDelete("admin/sohbet/mesajlar/{id:int}")]
        public async Task<IActionResult> MesajSil(int id)
        {
            var m = await _context.SohbetMesajlari.Include(x => x.Kullanici).FirstOrDefaultAsync(x => x.Id == id);
            if (m == null) return NotFound(new { message = "Mesaj bulunamadı." });
            if (!m.Silindi)
            {
                m.Silindi = true;
                await _context.SaveChangesAsync();
                await _hub.Clients.All.SendAsync("MesajSilindi", new { id });
                var ozet = m.Metin.Length > 100 ? m.Metin[..100] : m.Metin;
                await _denetim.KaydetAsync(User, "sohbet.mesajSil", $"Sohbet mesajı #{id} (kullanıcı #{m.KullaniciId} {m.Kullanici.KullaniciAdi})", ozet);
            }
            return Ok(new { message = "Mesaj silindi." });
        }

        [Yetki("sohbet.yonet")]
        [HttpPost("admin/sohbet/sustur")]
        public async Task<IActionResult> Sustur([FromBody] SohbetSusturRequest? istek)
        {
            if (istek == null || istek.KullaniciId <= 0) return BadRequest(new { message = "Kullanıcı kimliği zorunludur." });
            if (istek.Dakika < 1 || istek.Dakika > 10080) return BadRequest(new { message = "Süre 1 ile 10080 dakika arasında olmalıdır." });
            var neden = (istek.Neden ?? string.Empty).Trim();
            if (neden.Length > 200) return BadRequest(new { message = "Neden en fazla 200 karakter olabilir." });

            var hedef = await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == istek.KullaniciId);
            if (hedef == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            var benId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var b) ? b : 0;
            if (hedef.Id == benId) return BadRequest(new { message = "Kendinizi susturamazsınız." });
            if (Izinler.OwnerMi(hedef)) return StatusCode(403, new { message = "Site sahibi susturulamaz." });
            if (Izinler.AdminMi(hedef))
            {
                var ben = await _context.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == benId);
                if (ben == null || !Izinler.OwnerMi(ben)) return StatusCode(403, new { message = "Yöneticileri yalnızca site sahibi susturabilir." });
            }

            var bitis = DateTime.UtcNow.AddMinutes(istek.Dakika);
            var kayit = await _context.SohbetSusturmalari.FirstOrDefaultAsync(s => s.KullaniciId == hedef.Id);
            if (kayit == null) _context.SohbetSusturmalari.Add(new SohbetSusturma { KullaniciId = hedef.Id, Bitis = bitis, Neden = neden.Length == 0 ? null : neden, Tarih = DateTime.UtcNow });
            else { kayit.Bitis = bitis; kayit.Neden = neden.Length == 0 ? null : neden; kayit.Tarih = DateTime.UtcNow; }
            await _context.SaveChangesAsync();
            await _denetim.KaydetAsync(User, "sohbet.sustur", $"Kullanıcı #{hedef.Id} {hedef.KullaniciAdi}", $"{istek.Dakika} dk. {neden}".Trim());
            return Ok(new { message = "Kullanıcı sohbette susturuldu.", kullaniciId = hedef.Id, bitis = SohbetMetni.Utc(bitis) });
        }

        [Yetki("sohbet.yonet")]
        [HttpDelete("admin/sohbet/sustur/{kullaniciId:int}")]
        public async Task<IActionResult> SusturmaKaldir(int kullaniciId)
        {
            var adet = await _context.SohbetSusturmalari.Where(s => s.KullaniciId == kullaniciId).ExecuteDeleteAsync();
            if (adet == 0) return NotFound(new { message = "Bu kullanıcı susturulmuş değil." });
            await _denetim.KaydetAsync(User, "sohbet.susturKaldir", $"Kullanıcı #{kullaniciId}");
            return Ok(new { message = "Susturma kaldırıldı." });
        }

        [Yetki("sohbet.yonet")]
        [HttpGet("admin/sohbet/susturmalar")]
        public async Task<IActionResult> Susturmalar()
        {
            var simdi = DateTime.UtcNow;
            var liste = await _context.SohbetSusturmalari.AsNoTracking().Where(s => s.Bitis > simdi)
                .OrderBy(s => s.Bitis)
                .Join(_context.Kullanicilar, s => s.KullaniciId, k => k.Id, (s, k) => new { kullaniciId = s.KullaniciId, kullaniciAdi = k.KullaniciAdi, bitis = s.Bitis, neden = s.Neden })
                .ToListAsync();
            return Ok(liste.Select(x => new { x.kullaniciId, x.kullaniciAdi, bitis = SohbetMetni.Utc(x.bitis), x.neden }));
        }

        [Yetki("sohbet.yonet")]
        [HttpGet("admin/sohbet/mesajlar")]
        public async Task<IActionResult> YonetimListesi([FromQuery] string? arama = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        {
            if (page < 1) page = 1;
            if (page > 100000) page = 100000;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 100) pageSize = 100;
            var q = _context.SohbetMesajlari.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(arama))
            {
                var t = arama.Trim();
                if (t.Length > 100) t = t[..100];
                q = q.Where(m => m.Metin.Contains(t) || m.Kullanici.KullaniciAdi.Contains(t));
            }
            var toplam = await q.CountAsync();
            var simdi = DateTime.UtcNow;
            var liste = await q.OrderByDescending(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(m => new
                {
                    id = m.Id, kullaniciId = m.KullaniciId, kullaniciAdi = m.Kullanici.KullaniciAdi, metin = m.Metin,
                    stickerId = m.StickerId, tarih = m.Tarih, silindi = m.Silindi,
                    susturulmus = _context.SohbetSusturmalari.Any(s => s.KullaniciId == m.KullaniciId && s.Bitis > simdi)
                }).ToListAsync();
            Response.Headers["X-Total-Count"] = toplam.ToString();
            return Ok(liste.Select(m => new { m.id, m.kullaniciId, m.kullaniciAdi, m.metin, m.stickerId, tarih = SohbetMetni.Utc(m.tarih), m.silindi, m.susturulmus }));
        }
    }
}
