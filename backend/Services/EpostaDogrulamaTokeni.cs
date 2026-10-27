using System.Security.Cryptography;
using System.Text;

namespace Kage95.Backend.Services
{
    /// <summary>
    /// Veritabanı tablosu gerektirmeyen, imzalı ve süreli e-posta doğrulama belirteci.
    /// İçine e-posta adresinin parmak izi girer; adres değişince eski linkler kendiliğinden geçersizleşir.
    /// </summary>
    public static class EpostaDogrulamaTokeni
    {
        public static readonly TimeSpan Omur = TimeSpan.FromDays(3);

        public static string Uret(IConfiguration config, int kullaniciId, string email)
        {
            var bitis = DateTimeOffset.UtcNow.Add(Omur).ToUnixTimeSeconds();
            var yuk = $"{kullaniciId}.{bitis}";
            return $"{B64(Encoding.UTF8.GetBytes(yuk))}.{B64(Imza(config, yuk, email))}";
        }

        /// <summary>İmza kontrolünden önce kullanıcıyı bulmak için kimlik ve süreyi okur; süre dolmuşsa false döner.</summary>
        public static bool Cozumle(string? token, out int kullaniciId)
        {
            kullaniciId = 0;
            var p = token?.Split('.');
            if (p == null || p.Length != 2) return false;
            try
            {
                var a = Encoding.UTF8.GetString(B64Coz(p[0])).Split('.');
                if (a.Length != 2 || !int.TryParse(a[0], out var id) || !long.TryParse(a[1], out var bitis)) return false;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > bitis) return false;
                kullaniciId = id;
                return true;
            }
            catch { return false; }
        }

        public static bool ImzaDogru(IConfiguration config, string token, string email)
        {
            var p = token.Split('.');
            if (p.Length != 2) return false;
            try
            {
                var yuk = Encoding.UTF8.GetString(B64Coz(p[0]));
                return CryptographicOperations.FixedTimeEquals(Imza(config, yuk, email), B64Coz(p[1]));
            }
            catch { return false; }
        }

        private static byte[] Imza(IConfiguration config, string yuk, string email)
        {
            var gizli = config["JwtSettings:Secret"] ?? throw new InvalidOperationException("JwtSettings:Secret yok.");
            var parmakIzi = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())))[..16];
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(gizli + "|eposta-dogrulama"));
            return h.ComputeHash(Encoding.UTF8.GetBytes($"{yuk}|{parmakIzi}"));
        }

        private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] B64Coz(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
        }
    }
}
