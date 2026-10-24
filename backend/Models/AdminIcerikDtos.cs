namespace Kage95.Backend.Models
{
    public class BolumGuncelleRequest
    {
        public decimal? BolumNo { get; set; }
        public string? BolumAdi { get; set; }
    }

    public class SayfaSiralamaRequest
    {
        public List<int>? Idler { get; set; }
    }

    public class SayfaEkleRequest
    {
        public List<string>? Urller { get; set; }
    }

    public class SayfaGuncelleRequest
    {
        public string? ResimURL { get; set; }
    }

    public class AdminMangaOzetDto
    {
        public int Id { get; set; }
        public string MangaAdi { get; set; } = string.Empty;
        public string? KapakResmiURL { get; set; }
        public string Durum { get; set; } = string.Empty;
        public bool Yayinda { get; set; }
        public bool OneCikan { get; set; }
        public int BolumSayisi { get; set; }
        public DateTime EklenmeTarihi { get; set; }
        public DateTime GuncellenmeTarihi { get; set; }
    }

    public class AdminSayfaDto
    {
        public int Id { get; set; }
        public int SayfaNo { get; set; }
        public string ResimURL { get; set; } = string.Empty;
    }

    public class AdminBolumSayfalarDto
    {
        public int BolumId { get; set; }
        public int MangaId { get; set; }
        public string MangaAdi { get; set; } = string.Empty;
        public decimal BolumNo { get; set; }
        public string? BolumAdi { get; set; }
        public List<AdminSayfaDto> Sayfalar { get; set; } = new();
    }

    public class IstatistikGunDto
    {
        public string Tarih { get; set; } = string.Empty;
        public int YeniKullanici { get; set; }
        public int YeniYorum { get; set; }
        public int YeniBolum { get; set; }
    }

    public class IstatistikToplamDto
    {
        public int YeniKullanici { get; set; }
        public int YeniYorum { get; set; }
        public int YeniBolum { get; set; }
    }

    public class IstatistikDto
    {
        public List<IstatistikGunDto> Gunler { get; set; } = new();
        public IstatistikToplamDto Toplam { get; set; } = new();
    }
}
