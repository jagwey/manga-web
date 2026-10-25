using System;
using System.Collections.Generic;

namespace Kage95.Backend.Models
{
    public class Kullanici
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string SifreHash { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = "User"; // "Owner", "Admin" veya "User"
        // Virgülle ayrılmış izin anahtarları (yalnızca Admin için anlamlı; Owner'da tüm izinler örtüktür)
        public string? Yetkiler { get; set; }
        public bool Aktif { get; set; } = true;
        public DateTime? SonGirisTarihi { get; set; }
        // Ban bilgisi: Aktif=false iken anlamlıdır. BanBitis boşsa süresizdir; dolunca ban kendiliğinden kalkar.
        public string? BanNedeni { get; set; }
        public DateTime? BanTarihi { get; set; }
        public DateTime? BanBitis { get; set; }
        public int? BanYapanId { get; set; }
        // Artırılınca bu kullanıcının tüm eski oturum (JWT) belirteçleri geçersiz olur.
        public int TokenSurumu { get; set; } = 0;
        public string? ProfilResmiURL { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;

        public ICollection<Yorum> Yorumlar { get; set; } = new List<Yorum>();
        public ICollection<Favori> Favoriler { get; set; } = new List<Favori>();
        public ICollection<OkumaGecmisi> OkumaGecmisleri { get; set; } = new List<OkumaGecmisi>();
    }

    public class Manga
    {
        public int Id { get; set; }
        public string MangaAdi { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string? KapakResmiURL { get; set; }
        // İsteğe bağlı geniş görsel; ana sayfadaki öne çıkanlar şeridinde kapak yerine kullanılır
        public string? OneCikanResimURL { get; set; }
        public string? Yazar { get; set; }
        public string? Cizer { get; set; }
        public string Durum { get; set; } = "DevamEdiyor"; // "DevamEdiyor" / "Tamamlandi"
        public DateTime EklenmeTarihi { get; set; } = DateTime.UtcNow;
        public decimal? Puan { get; set; }
        public string? OkunmaSayisi { get; set; }
        public DateTime GuncellenmeTarihi { get; set; } = DateTime.UtcNow;
        // Yayında değilse herkese açık listelerde ve detayda görünmez (yalnızca yönetim görür).
        public bool Yayinda { get; set; } = true;
        public bool OneCikan { get; set; } = false;
        // Doluysa manga silinmiş sayılır ve her yerden gizlenir; bekleme süresi dolunca kalıcı silinir.
        public DateTime? SilinmeTarihi { get; set; }

        public ICollection<MangaKategori> MangaKategorileri { get; set; } = new List<MangaKategori>();
        public ICollection<Bolum> Bolumler { get; set; } = new List<Bolum>();
        public ICollection<Favori> Favoriler { get; set; } = new List<Favori>();
    }

    public class Kategori
    {
        public int Id { get; set; }
        public string KategoriAdi { get; set; } = string.Empty;

        public ICollection<MangaKategori> MangaKategorileri { get; set; } = new List<MangaKategori>();
    }

    public class MangaKategori
    {
        public int MangaId { get; set; }
        public Manga Manga { get; set; } = null!;

        public int KategoriId { get; set; }
        public Kategori Kategori { get; set; } = null!;
    }

    public class Bolum
    {
        public int Id { get; set; }
        public int MangaId { get; set; }
        public Manga Manga { get; set; } = null!;

        public decimal BolumNo { get; set; }
        public string? BolumAdi { get; set; }
        public DateTime YayinTarihi { get; set; } = DateTime.UtcNow;

        public ICollection<Sayfa> Sayfalar { get; set; } = new List<Sayfa>();
        public ICollection<Yorum> Yorumlar { get; set; } = new List<Yorum>();
    }

    public class Sayfa
    {
        public int Id { get; set; }
        public int BolumId { get; set; }
        public Bolum Bolum { get; set; } = null!;

        public int SayfaNo { get; set; }
        public string ResimURL { get; set; } = string.Empty;
    }

    public class Yorum
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;

        public int BolumId { get; set; }
        public Bolum Bolum { get; set; } = null!;

        public string Metin { get; set; } = string.Empty;
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
        public int BegeniSayisi { get; set; } = 0;
        public DateTime? Duzenlendi { get; set; }
        public bool Spoiler { get; set; }
        public int? YanitId { get; set; }
    }

    public class Favori
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;

        public int MangaId { get; set; }
        public Manga Manga { get; set; } = null!;

        public DateTime EklenmeTarihi { get; set; } = DateTime.UtcNow;
    }

    public class OkumaGecmisi
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;

        public int MangaId { get; set; }
        public Manga Manga { get; set; } = null!;

        public int SonOkunanBolumId { get; set; }
        public Bolum SonOkunanBolum { get; set; } = null!;

        public int SonSayfaNo { get; set; } = 1;
        public DateTime GuncellenmeTarihi { get; set; } = DateTime.UtcNow;
    }

    // Yönetim işlemlerinin denetim (kim, ne zaman, ne yaptı) kaydı
    public class DenetimKaydi
    {
        public int Id { get; set; }
        public int? YapanId { get; set; }
        public string YapanAdi { get; set; } = string.Empty;
        public string Eylem { get; set; } = string.Empty;   // örn. "manga.olustur", "kullanici.ban"
        public string Hedef { get; set; } = string.Empty;   // örn. "Manga #5 Örnek Manga"
        public string? Detay { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }

    // Anahtar/değer site ayarları (kayıt açık mı, duyuru, bakım modu vb.)
    public class SiteAyari
    {
        public string Anahtar { get; set; } = string.Empty;
        public string Deger { get; set; } = string.Empty;
    }

    // Seri (manga) yorumu
    public class MangaYorumu
    {
        public int Id { get; set; }
        public int MangaId { get; set; }
        public Manga Manga { get; set; } = null!;
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;
        public string Metin { get; set; } = string.Empty;
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
        public DateTime? Duzenlendi { get; set; }
        public bool Spoiler { get; set; }
        public int? YanitId { get; set; }
    }

    // Seri puanı (1-5), kullanıcı başına tek oy
    public class MangaPuani
    {
        public int Id { get; set; }
        public int MangaId { get; set; }
        public Manga Manga { get; set; } = null!;
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;
        public byte Deger { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }

    // Bölüm puanı (1-5), kullanıcı başına tek oy
    public class BolumPuani
    {
        public int Id { get; set; }
        public int BolumId { get; set; }
        public Bolum Bolum { get; set; } = null!;
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;
        public byte Deger { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }
    // Puan defteri (ledger): toplam puan = SUM(Puan). Silme/iptal ilgili satırın silinmesidir.
    public class PuanKaydi
    {
        public long Id { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;
        public string Eylem { get; set; } = string.Empty;
        public int Puan { get; set; }
        public string? Referans { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }

    // Yorum beğenisi (Tur: 'bolum' | 'manga')
    public class YorumBegeni
    {
        public int Id { get; set; }
        public string Tur { get; set; } = "bolum";
        public int YorumId { get; set; }
        public int KullaniciId { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }

    // Yorum şikayeti (Durum: 'acik' | 'kapali')
    public class YorumSikayeti
    {
        public int Id { get; set; }
        public string Tur { get; set; } = "bolum";
        public int YorumId { get; set; }
        public int SikayetEdenId { get; set; }
        public string Neden { get; set; } = string.Empty;
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
        public string Durum { get; set; } = "acik";
    }

    public class Duyuru
    {
        public int Id { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string Etiket { get; set; } = "bilgi"; // bilgi | etkinlik | yeni | uyari
        public string? Baglanti { get; set; }
        public bool Aktif { get; set; } = true;
        public int Sira { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
        public DateTime? BitisTarihi { get; set; }
    }

    public class SohbetMesaji
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;
        public string Metin { get; set; } = string.Empty;
        public string? StickerId { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
        public bool Silindi { get; set; }
    }

    public class SohbetSusturma
    {
        public int KullaniciId { get; set; }
        public DateTime Bitis { get; set; }
        public string? Neden { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }

    public class KullaniciTercihi
    {
        public int KullaniciId { get; set; }
        public string? AdRengi { get; set; }
        public string? MesajFontu { get; set; }
        public bool Parlak { get; set; }
        public bool Animasyon { get; set; }
        public bool Rozet { get; set; }
        public string? Unvan { get; set; }
    }

    public class Bildirim
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public string Tur { get; set; } = "bilgi"; // bolum | yanit | etiket
        public string Baslik { get; set; } = string.Empty;
        public string? Baglanti { get; set; }
        public bool Okundu { get; set; }
        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }
}
