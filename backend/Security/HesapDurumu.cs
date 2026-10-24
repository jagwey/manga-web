using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;

namespace Kage95.Backend.Security
{
    /// <summary>
    /// Ban durumu mantığı TEK yerde: Aktif=false ise banlı; BanBitis doluysa ve geçmişteyse
    /// ban kendiliğinden kalkmış sayılır (ilk kontrolde Aktif=true yapılıp ban alanları temizlenir).
    /// Login, JWT doğrulama, [Yetki] filtresi ve admin listeleri bu sınıfı kullanır.
    /// </summary>
    public static class HesapDurumu
    {
        public static bool BanSuresiDoldu(Kullanici k)
            => !k.Aktif && k.BanBitis.HasValue && k.BanBitis.Value <= DateTime.UtcNow;

        /// <summary>Süresi dolmamış (geçerli) ban var mı. Bellekteki nesneyi değiştirmez.</summary>
        public static bool BanliMi(Kullanici k) => !k.Aktif && !BanSuresiDoldu(k);

        public static void BaniTemizle(Kullanici k)
        {
            k.Aktif = true;
            k.BanNedeni = null;
            k.BanTarihi = null;
            k.BanBitis = null;
            k.BanYapanId = null;
        }

        /// <summary>
        /// Banlı mı döner. Ban süresi dolmuşsa veritabanında ve bellekteki nesnede temizler (false döner).
        /// </summary>
        public static async Task<bool> BanliMiAsync(AppDbContext db, Kullanici k, CancellationToken ct = default)
        {
            if (k.Aktif) return false;
            if (!BanSuresiDoldu(k)) return true;

            await SuresiDolanlariTemizleAsync(db, k.Id, ct);
            BaniTemizle(k);
            return false;
        }

        /// <summary>Süresi dolmuş banları toplu (ya da tek kullanıcı için) temizler.</summary>
        public static Task<int> SuresiDolanlariTemizleAsync(AppDbContext db, int? kullaniciId = null, CancellationToken ct = default)
            => db.Kullanicilar
                .Where(x => !x.Aktif && x.BanBitis != null && x.BanBitis <= DateTime.UtcNow && (kullaniciId == null || x.Id == kullaniciId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Aktif, true)
                    .SetProperty(x => x.BanNedeni, (string?)null)
                    .SetProperty(x => x.BanTarihi, (DateTime?)null)
                    .SetProperty(x => x.BanBitis, (DateTime?)null)
                    .SetProperty(x => x.BanYapanId, (int?)null), ct);
    }
}
