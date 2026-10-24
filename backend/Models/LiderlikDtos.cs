namespace Kage95.Backend.Models
{
    public class LiderlikSatiriDto
    {
        public int Sira { get; set; }
        public int KullaniciId { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string? ProfilResmiURL { get; set; }
        public int Puan { get; set; }
        public string Rutbe { get; set; } = string.Empty;
        public int Seviye { get; set; }
        public AdStiliDto? AdStili { get; set; }
    }

    public class LiderlikBenDto
    {
        public int? Sira { get; set; }
        public int Puan { get; set; }
        public string Rutbe { get; set; } = string.Empty;
        public string? SonrakiRutbe { get; set; }
        public int SonrakiRutbeIcinKalan { get; set; }
        public int Seviye { get; set; }
        public int SeviyeIcinGereken { get; set; }
        public int SonrakiSeviyeIcinKalan { get; set; }
    }

    public class LiderlikYanitiDto
    {
        public string Donem { get; set; } = "tum";
        public DateTime Guncelleme { get; set; }
        public List<LiderlikSatiriDto> Liste { get; set; } = new();
        public LiderlikBenDto? Ben { get; set; }
    }

    public class PuanKuraliDto
    {
        public string Eylem { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public int Puan { get; set; }
        public string Kural { get; set; } = string.Empty;
    }
}
