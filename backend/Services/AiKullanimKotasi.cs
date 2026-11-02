using Microsoft.Extensions.Caching.Memory;
namespace Kage95.Backend.Services;

// Premium hakları yalnızca veritabanından gelir; istemci plan bildiremez.
// Tek backend örneği içindir. Çoklu örnekte atomik Redis/DB sayacıyla değiştirin.
public sealed class AiKullanimKotasi(IMemoryCache cache, IConfiguration config)
{
    private readonly object gate = new();
    public bool Kullan(string kimlik, int? userId, bool premium = false)
    {
        var bolum = kimlik.StartsWith("bolum:", StringComparison.Ordinal);
        var limit = Math.Clamp(config.GetValue<int?>(bolum ? "KageAi:BolumGunlukLimit" : premium ? "KageAi:PremiumGunlukLimit" : userId.HasValue ? "KageAi:UyeGunlukLimit" : "KageAi:MisafirGunlukLimit") ?? (bolum ? 10 : premium ? 50 : userId.HasValue ? 10 : 3), 0, 500);
        var now = DateTimeOffset.UtcNow;
        var key = $"ai-kota:{now:yyyyMMdd}:{kimlik}";
        lock (gate)
        {
            var used = cache.Get<int>(key);
            if (used >= limit) return false;
            cache.Set(key, used + 1, new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero));
            return true;
        }
    }
}
