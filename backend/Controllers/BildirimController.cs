using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;

namespace Kage95.Backend.Controllers
{
    public class BildirimOkunduRequest
    {
        public int? Id { get; set; }
    }

    [ApiController]
    [Route("api/bildirimler")]
    [Authorize]
    public class BildirimController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BildirimController(AppDbContext db)
        {
            _db = db;
        }

        private int? KullaniciId()
        {
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idStr, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> Listele([FromQuery] int limit = 20)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });

            limit = Math.Clamp(limit, 1, 50);
            var okunmamisSayisi = await _db.Bildirimler.AsNoTracking()
                .CountAsync(b => b.KullaniciId == uid.Value && !b.Okundu);

            var liste = await _db.Bildirimler.AsNoTracking()
                .Where(b => b.KullaniciId == uid.Value)
                .OrderByDescending(b => b.Tarih)
                .ThenByDescending(b => b.Id)
                .Take(limit)
                .Select(b => new
                {
                    id = b.Id,
                    tur = b.Tur,
                    baslik = b.Baslik,
                    baglanti = b.Baglanti,
                    okundu = b.Okundu,
                    tarih = b.Tarih
                })
                .ToListAsync();

            return Ok(new { okunmamisSayisi, liste });
        }

        [HttpPost("okundu")]
        public async Task<IActionResult> OkunduIsaretle([FromBody] BildirimOkunduRequest? istek)
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });

            var sorgu = _db.Bildirimler.Where(b => b.KullaniciId == uid.Value && !b.Okundu);
            if (istek?.Id != null)
                sorgu = sorgu.Where(b => b.Id == istek.Id.Value);

            await sorgu.ExecuteUpdateAsync(s => s.SetProperty(b => b.Okundu, true));
            return Ok(new { message = "Bildirimler okundu olarak işaretlendi." });
        }

        [HttpDelete]
        public async Task<IActionResult> Temizle()
        {
            var uid = KullaniciId();
            if (uid == null) return Unauthorized(new { message = "Oturumunuz geçersiz." });

            await _db.Bildirimler.Where(b => b.KullaniciId == uid.Value).ExecuteDeleteAsync();
            return Ok(new { message = "Bildirimler temizlendi." });
        }

        /// <summary>Verilen alıcılara bildirim kaydı ekler; hata olursa ana işlemi kesmez.</summary>
        public static async Task BildirimKaydetAsync(AppDbContext db, IEnumerable<int> aliciIdler, string tur, string baslik, string? baglanti)
        {
            try
            {
                var idler = aliciIdler.Where(x => x > 0).Distinct().ToList();
                if (idler.Count == 0 || string.IsNullOrWhiteSpace(baslik)) return;

                var temizBaslik = baslik.Trim();
                if (temizBaslik.Length > 250) temizBaslik = temizBaslik[..250];
                var temizTur = string.IsNullOrWhiteSpace(tur) ? "bilgi" : tur.Trim();
                if (temizTur.Length > 20) temizTur = temizTur[..20];
                var temizBaglanti = string.IsNullOrWhiteSpace(baglanti) ? null : baglanti.Trim();
                if (temizBaglanti != null && temizBaglanti.Length > 300) temizBaglanti = temizBaglanti[..300];

                var simdi = DateTime.UtcNow;
                foreach (var uid in idler)
                {
                    db.Bildirimler.Add(new Bildirim
                    {
                        KullaniciId = uid,
                        Tur = temizTur,
                        Baslik = temizBaslik,
                        Baglanti = temizBaglanti,
                        Okundu = false,
                        Tarih = simdi
                    });
                }
                await db.SaveChangesAsync();
            }
            catch
            {
                // Bildirim kaydı hatası ana işlemi engellememeli
            }
        }
    }
}
