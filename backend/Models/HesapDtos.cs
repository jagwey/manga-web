namespace Kage95.Backend.Models
{
    // --- Yönetim: kullanıcı uçları ---
    public class KullaniciDuzenleRequest
    {
        public string? KullaniciAdi { get; set; }
        public string? Email { get; set; }
    }

    public class SifreSifirlaRequest
    {
        public string? YeniSifre { get; set; }
    }

    public class BanRequest
    {
        public string? Neden { get; set; }
        public DateTimeOffset? Bitis { get; set; }
    }

    public class AyarlarGuncelleRequest
    {
        public bool? KayitAcik { get; set; }
        public bool? YorumAcik { get; set; }
        public bool? BakimModu { get; set; }
        public string? BakimMesaji { get; set; }
        public bool? DuyuruAcik { get; set; }
        public string? DuyuruMetni { get; set; }
        public bool? SohbetAcik { get; set; }
    }

    // --- Hesabım ---
    public class ProfilGuncelleRequest
    {
        public string? KullaniciAdi { get; set; }
        public string? Email { get; set; }
        public string? MevcutSifre { get; set; }
    }

    public class SifreDegistirRequest
    {
        public string? MevcutSifre { get; set; }
        public string? YeniSifre { get; set; }
    }

    public class HesapSilRequest
    {
        public string? Sifre { get; set; }
    }

    /// <summary>Avatar yükleme formu (multipart alanı: dosya). Swashbuckle için IFormFile bir sınıfa sarılır.</summary>
    public class AvatarFormu
    {
        public IFormFile? Dosya { get; set; }
    }
}
