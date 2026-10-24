namespace Kage95.Backend.Models
{
    public class CategoryRequest
    {
        public string KategoriAdi { get; set; } = string.Empty;
    }

    // Yönetim paneli DTO'ları
    public class YoneticiDto
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public List<string> Yetkiler { get; set; } = new();
        public bool Aktif { get; set; }
        public DateTime KayitTarihi { get; set; }
    }

    public class YoneticiEkleRequest
    {
        public string KullaniciAdiOrEmail { get; set; } = string.Empty;
        public List<string>? Yetkiler { get; set; }
    }

    public class YoneticiGuncelleRequest
    {
        public List<string>? Yetkiler { get; set; }
    }

    public class DurumRequest
    {
        public bool Aktif { get; set; }
    }
}
