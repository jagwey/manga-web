using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Backend.Services
{
    public interface ISiteAyarServisi
    {
        Task<string?> DegerAsync(string anahtar);
        /// <summary>Değer "1" ise true; kayıt yoksa varsayılan döner.</summary>
        Task<bool> AcikMiAsync(string anahtar, bool varsayilan = true);
        Task<Dictionary<string, string>> TumuAsync();
        Task KaydetAsync(string anahtar, string deger);
    }

    /// <summary>Anahtar/değer site ayarları (SiteAyarlari tablosu). Anahtarlar: kayitAcik, yorumAcik, bakimModu, bakimMesaji, duyuruAcik, duyuruMetni.</summary>
    public class SiteAyarServisi : ISiteAyarServisi
    {
        private readonly AppDbContext _context;

        public SiteAyarServisi(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> DegerAsync(string anahtar)
        {
            return await _context.SiteAyarlari.AsNoTracking()
                .Where(a => a.Anahtar == anahtar).Select(a => a.Deger).FirstOrDefaultAsync();
        }

        public async Task<bool> AcikMiAsync(string anahtar, bool varsayilan = true)
        {
            var deger = await DegerAsync(anahtar);
            return deger == null ? varsayilan : deger == "1";
        }

        public async Task<Dictionary<string, string>> TumuAsync()
        {
            return await _context.SiteAyarlari.AsNoTracking().ToDictionaryAsync(a => a.Anahtar, a => a.Deger);
        }

        public async Task KaydetAsync(string anahtar, string deger)
        {
            var kayit = await _context.SiteAyarlari.FirstOrDefaultAsync(a => a.Anahtar == anahtar);
            if (kayit == null) _context.SiteAyarlari.Add(new SiteAyari { Anahtar = anahtar, Deger = deger });
            else kayit.Deger = deger;
            await _context.SaveChangesAsync();
        }
    }
}
