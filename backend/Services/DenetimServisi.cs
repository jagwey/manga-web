using System.Security.Claims;
using Kage95.Backend.Data;
using Kage95.Backend.Models;

namespace Kage95.Backend.Services
{
    public interface IDenetimServisi
    {
        /// <summary>
        /// Bir yönetim işlemini denetim kaydına yazar. İşlemin kendi SaveChanges çağrısından SONRA çağırın.
        /// </summary>
        Task KaydetAsync(ClaimsPrincipal? yapan, string eylem, string hedef, string? detay = null);
    }

    public class DenetimServisi : IDenetimServisi
    {
        private readonly AppDbContext _context;

        public DenetimServisi(AppDbContext context)
        {
            _context = context;
        }

        public async Task KaydetAsync(ClaimsPrincipal? yapan, string eylem, string hedef, string? detay = null)
        {
            int? yapanId = null;
            var idDeger = yapan?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idDeger, out var id)) yapanId = id;

            _context.DenetimKayitlari.Add(new DenetimKaydi
            {
                YapanId = yapanId,
                YapanAdi = Trunc(yapan?.FindFirst(ClaimTypes.Name)?.Value ?? "sistem", 50),
                Eylem = Trunc(eylem, 100),
                Hedef = Trunc(hedef, 200),
                Detay = detay == null ? null : Trunc(detay, 1000),
                Tarih = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        private static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n);
    }
}
