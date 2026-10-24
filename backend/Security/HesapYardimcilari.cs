using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;

namespace Kage95.Backend.Security
{
    /// <summary>Kullanıcı adı / e-posta / şifre kuralları ve parola doğrulama.</summary>
    public static class HesapKurallari
    {
        private static readonly Regex KullaniciAdiRegex = new(@"^[\p{L}\p{Nd}_.\-]{3,30}$", RegexOptions.Compiled);

        public static string? KullaniciAdiHata(string? ad)
            => ad != null && KullaniciAdiRegex.IsMatch(ad)
                ? null
                : "Kullanıcı adı 3-30 karakter olmalı; yalnızca harf, rakam, _ . - içerebilir.";

        public static string? EmailHata(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 100) return "Geçerli bir e-posta adresi girin (en fazla 100 karakter).";
            try
            {
                var a = new MailAddress(email);
                if (!string.Equals(a.Address, email, StringComparison.OrdinalIgnoreCase) || !a.Host.Contains('.'))
                    return "Geçerli bir e-posta adresi girin.";
            }
            catch { return "Geçerli bir e-posta adresi girin."; }
            return null;
        }

        public static string? SifreHata(string? sifre, string kullaniciAdi)
        {
            if (string.IsNullOrEmpty(sifre) || sifre.Length < 8) return "Şifre en az 8 karakter olmalıdır.";
            if (sifre.Length > 128) return "Şifre en fazla 128 karakter olabilir.";
            if (string.Equals(sifre, kullaniciAdi, StringComparison.OrdinalIgnoreCase)) return "Şifre kullanıcı adından farklı olmalıdır.";
            return null;
        }

        /// <summary>BCrypt ya da eski SHA256 hex hash ile doğrular (düz metin karşılaştırması yok).</summary>
        public static bool SifreDogru(Kullanici k, string sifre, out bool eskiHash)
        {
            eskiHash = false;
            var hash = k.SifreHash ?? string.Empty;
            if (hash.StartsWith("$2", StringComparison.Ordinal))
            {
                try { return BCrypt.Net.BCrypt.Verify(sifre, hash); } catch { return false; }
            }
            var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sifre)));
            var ok = hash.Length == sha.Length && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(hash.ToUpperInvariant()), Encoding.ASCII.GetBytes(sha));
            eskiHash = ok;
            return ok;
        }

        /// <summary>Güçlü rastgele geçici şifre (16 karakter, karışık).</summary>
        public static string GeciciSifreUret()
        {
            const string kucuk = "abcdefghijkmnpqrstuvwxyz", buyuk = "ABCDEFGHJKLMNPQRSTUVWXYZ", rakam = "23456789", ozel = "!@#$%*?";
            var tum = kucuk + buyuk + rakam + ozel;
            var k = new List<char>
            {
                kucuk[RandomNumberGenerator.GetInt32(kucuk.Length)],
                buyuk[RandomNumberGenerator.GetInt32(buyuk.Length)],
                rakam[RandomNumberGenerator.GetInt32(rakam.Length)],
                ozel[RandomNumberGenerator.GetInt32(ozel.Length)]
            };
            while (k.Count < 16) k.Add(tum[RandomNumberGenerator.GetInt32(tum.Length)]);
            for (var i = k.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (k[i], k[j]) = (k[j], k[i]);
            }
            return new string(k.ToArray());
        }
    }

    /// <summary>Avatar dosyaları: wwwroot/avatars/(guid).(jpg|png|webp)</summary>
    public static class AvatarYardimcisi
    {
        public const long MaksBoyut = 2L * 1024 * 1024;
        private static readonly Regex AvatarYolu = new(@"/avatars/(?<ad>[0-9a-f]{32}\.(jpg|png|webp))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Klasor(IWebHostEnvironment env)
            => Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "avatars");

        /// <summary>URL bizim avatar biçimindeyse fiziksel dosyayı siler (yol kaçışı regex ile engellenir).</summary>
        public static void DosyayiSil(IWebHostEnvironment env, string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            var m = AvatarYolu.Match(url);
            if (!m.Success) return;
            try
            {
                var yol = Path.Combine(Klasor(env), m.Groups["ad"].Value.ToLowerInvariant());
                if (File.Exists(yol)) File.Delete(yol);
            }
            catch { /* dosya silinemese de işlem sürer */ }
        }

        /// <summary>Başlık baytlarından gerçek görsel türünü bulur: "jpg", "png", "webp" ya da null.</summary>
        public static string? TurBul(byte[] h, int len)
        {
            if (len >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return "jpg";
            if (len >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return "png";
            if (len >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return "webp";
            return null;
        }
    }

    public static class KullaniciSilici
    {
        /// <summary>Kullanıcıyı ve bağlı verisini (yorum, favori, okuma geçmişi) tek transaction'da siler.</summary>
        public static async Task SilAsync(AppDbContext db, IWebHostEnvironment env, Kullanici hedef)
        {
            var avatar = hedef.ProfilResmiURL;
            var id = hedef.Id;
            await using var tx = await db.Database.BeginTransactionAsync();
            // Kullanıcının yorumlarına bağlı yanıtlar, beğeniler ve şikayetler (YanitId için FK yok, elle temizlenir)
            var bolumYorumIdleri = db.Yorumlar.Where(y => y.KullaniciId == id).Select(y => y.Id);
            var mangaYorumIdleri = db.MangaYorumlari.Where(y => y.KullaniciId == id).Select(y => y.Id);
            await db.YorumBegenileri.Where(b => b.KullaniciId == id).ExecuteDeleteAsync();
            await db.YorumSikayetleri.Where(b => b.SikayetEdenId == id).ExecuteDeleteAsync();
            await db.YorumBegenileri.Where(b => (b.Tur == "bolum" && bolumYorumIdleri.Contains(b.YorumId)) || (b.Tur == "manga" && mangaYorumIdleri.Contains(b.YorumId))).ExecuteDeleteAsync();
            await db.YorumSikayetleri.Where(b => (b.Tur == "bolum" && bolumYorumIdleri.Contains(b.YorumId)) || (b.Tur == "manga" && mangaYorumIdleri.Contains(b.YorumId))).ExecuteDeleteAsync();
            await db.Yorumlar.Where(y => y.YanitId != null && bolumYorumIdleri.Contains(y.YanitId.Value)).ExecuteDeleteAsync();
            await db.MangaYorumlari.Where(y => y.YanitId != null && mangaYorumIdleri.Contains(y.YanitId.Value)).ExecuteDeleteAsync();
            await db.SohbetMesajlari.Where(m => m.KullaniciId == id).ExecuteDeleteAsync();
            await db.SohbetSusturmalari.Where(s => s.KullaniciId == id).ExecuteDeleteAsync();
            await db.KullaniciTercihleri.Where(t => t.KullaniciId == id).ExecuteDeleteAsync();
            await db.Bildirimler.Where(b => b.KullaniciId == id).ExecuteDeleteAsync();
            await db.Yorumlar.Where(y => y.KullaniciId == id).ExecuteDeleteAsync();
            await db.MangaYorumlari.Where(y => y.KullaniciId == id).ExecuteDeleteAsync();
            await db.MangaPuanlari.Where(p => p.KullaniciId == id).ExecuteDeleteAsync();
            await db.BolumPuanlari.Where(p => p.KullaniciId == id).ExecuteDeleteAsync();
            await db.Favoriler.Where(f => f.KullaniciId == id).ExecuteDeleteAsync();
            await db.OkumaGecmisi.Where(o => o.KullaniciId == id).ExecuteDeleteAsync();
            await db.PuanKayitlari.Where(p => p.KullaniciId == id).ExecuteDeleteAsync();
            await db.Kullanicilar.Where(k => k.BanYapanId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.BanYapanId, (int?)null));
            await db.Kullanicilar.Where(k => k.Id == id).ExecuteDeleteAsync();
            await tx.CommitAsync();
            AvatarYardimcisi.DosyayiSil(env, avatar);
        }
    }
}
