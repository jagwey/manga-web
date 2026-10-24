using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Kage95.Backend.Data;
using Kage95.Backend.Models;

namespace Kage95.Backend.Services
{
    /// <summary>Kullanıcı kozmetik tercihlerini seviyeye göre doğrular ve toplu çözer (adStili).</summary>
    public class TercihServisi
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _onbellek;

        public TercihServisi(AppDbContext db, IMemoryCache onbellek)
        {
            _db = db;
            _onbellek = onbellek;
        }

        public async Task<int> ToplamPuanAsync(int kullaniciId)
            => await _db.PuanKayitlari.AsNoTracking().Where(p => p.KullaniciId == kullaniciId && p.Puan > 0).SumAsync(p => (int?)p.Puan) ?? 0;

        /// <summary>Kayıtlı tercihten, verilen seviyede GEÇERLİ olanları süzer; hiçbiri yoksa null.</summary>
        public static AdStiliDto? Suz(KullaniciTercihi t, int seviye)
        {
            var s = new AdStiliDto();
            var renk = OdulKatalogu.Renk(t.AdRengi);
            if (renk != null && seviye >= renk.Seviye) { s.AdRengi = renk.Id; s.AdRengiHex = renk.Hex; }
            var font = OdulKatalogu.Font(t.MesajFontu);
            if (font != null && font.Id != "varsayilan" && seviye >= font.Seviye) s.MesajFontu = font.Id;
            if (t.Parlak && seviye >= OdulKatalogu.ParlakSeviye) s.Parlak = true;
            if (t.Animasyon && seviye >= OdulKatalogu.AnimasyonSeviye) s.Animasyon = true;
            if (t.Rozet && seviye >= OdulKatalogu.RozetSeviye) s.Rozet = true;
            if (!string.IsNullOrWhiteSpace(t.Unvan) && seviye >= OdulKatalogu.UnvanSeviye && OdulKatalogu.UnvanHatasi(t.Unvan) == null)
                s.Unvan = t.Unvan.Trim();
            return s.AdRengi == null && s.MesajFontu == null && s.Parlak == null && s.Animasyon == null && s.Rozet == null && s.Unvan == null ? null : s;
        }

        private sealed record Giris(AdStiliDto? Stil);

        /// <summary>Kullanıcı kimlikleri için adStili sözlüğü (yalnızca stili olanlar). Tek toplu sorgu + 60 sn önbellek.</summary>
        public async Task<Dictionary<int, AdStiliDto>> CozAsync(IEnumerable<int> idler)
        {
            var sonuc = new Dictionary<int, AdStiliDto>();
            var eksik = new List<int>();
            var surum = PuanServisi.Surum;
            foreach (var id in idler.Distinct())
            {
                if (_onbellek.TryGetValue($"tercih:{id}:{surum}", out Giris? g) && g != null)
                { if (g.Stil != null) sonuc[id] = g.Stil; }
                else eksik.Add(id);
            }
            if (eksik.Count == 0) return sonuc;

            var tercihler = await _db.KullaniciTercihleri.AsNoTracking().Where(t => eksik.Contains(t.KullaniciId)).ToListAsync();
            var tIdler = tercihler.Select(t => t.KullaniciId).ToList();
            var puanlar = tercihler.Count == 0 ? new Dictionary<int, int>()
                : await _db.PuanKayitlari.AsNoTracking()
                    .Where(p => p.Puan > 0 && tIdler.Contains(p.KullaniciId))
                    .GroupBy(p => p.KullaniciId).Select(g => new { g.Key, T = g.Sum(x => x.Puan) })
                    .ToDictionaryAsync(x => x.Key, x => x.T);
            var tMap = tercihler.ToDictionary(t => t.KullaniciId);
            foreach (var id in eksik)
            {
                AdStiliDto? stil = null;
                if (tMap.TryGetValue(id, out var t))
                    stil = Suz(t, PuanServisi.Seviye(puanlar.GetValueOrDefault(id)));
                _onbellek.Set($"tercih:{id}:{surum}", new Giris(stil), TimeSpan.FromSeconds(60));
                if (stil != null) sonuc[id] = stil;
            }
            return sonuc;
        }

        public async Task<AdStiliDto?> TekAsync(int kullaniciId)
            => (await CozAsync(new[] { kullaniciId })).GetValueOrDefault(kullaniciId);

        /// <summary>Tercih değiştiğinde önbelleği geçersiz kılar (puan sürümünü artırmadan tek kullanıcı için).</summary>
        public void Unut(int kullaniciId) => _onbellek.Remove($"tercih:{kullaniciId}:{PuanServisi.Surum}");
    }
}
