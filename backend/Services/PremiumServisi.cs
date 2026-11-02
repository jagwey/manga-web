using Kage95.Backend.Data;
using Microsoft.EntityFrameworkCore;
namespace Kage95.Backend.Services;
public sealed class PremiumServisi(AppDbContext db)
{
    public Task<bool> Etkin(int userId, CancellationToken ct) => db.PremiumUyelikler.AsNoTracking()
        .AnyAsync(p => p.KullaniciId == userId && !p.IptalEdildi && p.BitisUtc > DateTime.UtcNow, ct);
}
