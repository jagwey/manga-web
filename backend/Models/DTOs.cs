using System;
using System.Collections.Generic;

namespace Kage95.Backend.Models
{
    // Auth DTOs
    public class RegisterRequest
    {
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string KullaniciAdiOrEmail { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? ProfilResmiURL { get; set; }
        public List<string> Yetkiler { get; set; } = new();
    }

    public class UserProfileResponse
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public DateTime KayitTarihi { get; set; }
        public List<string> Yetkiler { get; set; } = new();
    }

    // Manga DTOs
    public class MangaListDto
    {
        public int Id { get; set; }
        public string MangaAdi { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? Aciklama { get; set; }
        public string? KapakResmiURL { get; set; }
        public string? Yazar { get; set; }
        public string? Cizer { get; set; }
        public string Durum { get; set; } = string.Empty;
        public DateTime EklenmeTarihi { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        public decimal? Puan { get; set; }
        public int OySayisi { get; set; }
        public string? OkunmaSayisi { get; set; }
        public List<string> Kategoriler { get; set; } = new();
        public int BolumSayisi { get; set; }
        public bool Yayinda { get; set; } = true;
        public bool OneCikan { get; set; }
        public int? IlkBolumId { get; set; }
        public int? SonBolumId { get; set; }
        public decimal? SonBolumNo { get; set; }
        public DateTime? SonBolumTarihi { get; set; }
        // Yalnızca /api/mangas/populer yanıtında dolar
        public int? Okunma { get; set; }
    }

    public class MangaDetailDto
    {
        public int Id { get; set; }
        public string MangaAdi { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? Aciklama { get; set; }
        public string? KapakResmiURL { get; set; }
        public string? Yazar { get; set; }
        public string? Cizer { get; set; }
        public string Durum { get; set; } = string.Empty;
        public DateTime EklenmeTarihi { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        public decimal? Puan { get; set; }
        public int OySayisi { get; set; }
        public string? OkunmaSayisi { get; set; }
        public List<string> Kategoriler { get; set; } = new();
        public List<BolumSummaryDto> Bolumler { get; set; } = new();
        public bool Yayinda { get; set; } = true;
        public bool OneCikan { get; set; }
        public string? OneCikanResimURL { get; set; }
    }

    public class BolumSummaryDto
    {
        public int Id { get; set; }
        public decimal BolumNo { get; set; }
        public string? BolumAdi { get; set; }
        public DateTime YayinTarihi { get; set; }
        public int SayfaSayisi { get; set; }
    }

    public class CreateMangaRequest
    {
        public string MangaAdi { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string? KapakResmiURL { get; set; }
        public string? Yazar { get; set; }
        public string? Cizer { get; set; }
        public string Durum { get; set; } = "DevamEdiyor";
        public List<string> Kategoriler { get; set; } = new();
        public bool? Yayinda { get; set; }
        public bool? OneCikan { get; set; }
        public string? OneCikanResimURL { get; set; }
    }

    public class UpdateMangaRequest
    {
        public string MangaAdi { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string? KapakResmiURL { get; set; }
        public string? Yazar { get; set; }
        public string? Cizer { get; set; }
        public string? Durum { get; set; }
        // null ise kategoriler değiştirilmez
        public List<string>? Kategoriler { get; set; }
        // null ise değiştirilmez
        public bool? Yayinda { get; set; }
        public bool? OneCikan { get; set; }
        // null ise değiştirilmez, boş metin görseli kaldırır
        public string? OneCikanResimURL { get; set; }
    }

    // Chapter & Pages DTOs
    public class ChapterPagesResponse
    {
        public int MangaId { get; set; }
        public string MangaAdi { get; set; } = string.Empty;
        public int BolumId { get; set; }
        public decimal BolumNo { get; set; }
        public string? BolumAdi { get; set; }
        public List<string> Sayfalar { get; set; } = new();
        public int? OncekiBolumId { get; set; }
        public int? SonrakiBolumId { get; set; }
    }

    // admin.html formatıyla tam uyumlu DTO:
    // admin.html gönderimi: { mangaAdi, bolumNo, bolumAdi, kapakGorseli, sayfalar }
    public class CreateChapterWithPagesRequest
    {
        public int? MangaId { get; set; }
        public string? MangaAdi { get; set; }
        public decimal BolumNo { get; set; }
        public string? BolumAdi { get; set; }
        public string? KapakGorseli { get; set; }
        // Hem string (virgülle ayrılmış) hem List<string> kabul edilebilir
        public object? Sayfalar { get; set; }
    }

    // Comment DTOs
    public class YorumDto
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Metin { get; set; } = string.Empty;
        public DateTime Tarih { get; set; }
        public int BegeniSayisi { get; set; }
        public int PuanKazanildi { get; set; }
        public bool BenBegendim { get; set; }
        public bool BenimYorumum { get; set; }
        public DateTime? Duzenlendi { get; set; }
        public bool Spoiler { get; set; }
        public int? YanitId { get; set; }
        public int YanitSayisi { get; set; }
        public AdStiliDto? AdStili { get; set; }
    }

    public class CreateYorumRequest
    {
        public string Metin { get; set; } = string.Empty;
        public bool? Spoiler { get; set; }
        public int? YanitId { get; set; }
    }

    public class YorumGuncelleRequest
    {
        public string? Metin { get; set; }
        public bool? Spoiler { get; set; }
    }

    public class YorumSikayetRequest
    {
        public string? Neden { get; set; }
    }

    // Library & Progress DTOs
    public class ToggleLibraryRequest
    {
        public int MangaId { get; set; }
    }

    public class SaveProgressRequest
    {
        public int MangaId { get; set; }
        public int BolumId { get; set; }
        public int SayfaNo { get; set; }
    }

    public class ProgressDto
    {
        public int MangaId { get; set; }
        public string MangaAdi { get; set; } = string.Empty;
        public string? KapakResmiURL { get; set; }
        public int SonOkunanBolumId { get; set; }
        public decimal SonOkunanBolumNo { get; set; }
        public string? SonOkunanBolumAdi { get; set; }
        public int SonSayfaNo { get; set; }
        public DateTime GuncellenmeTarihi { get; set; }
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string KategoriAdi { get; set; } = string.Empty;
        public int MangaSayisi { get; set; }
    }
}

