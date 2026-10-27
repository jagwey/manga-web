using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Services;
using Kage95.Backend.Security;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly ISiteAyarServisi _ayarlar;
        private readonly IGirisSinirlayici _sinirlayici;
        private readonly IEmailService _eposta;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _log;

        public AuthController(AppDbContext context, ITokenService tokenService, ISiteAyarServisi ayarlar, IGirisSinirlayici sinirlayici,
            IEmailService eposta, IConfiguration config, ILogger<AuthController> log)
        {
            _eposta = eposta;
            _config = config;
            _log = log;
            _context = context;
            _tokenService = tokenService;
            _ayarlar = ayarlar;
            _sinirlayici = sinirlayici;
        }

        /// <summary>
        /// Şifremi unuttum: e-posta kayıtlıysa 30 dakika geçerli sıfırlama bağlantısı gönderir.
        /// Hesap varlığı sızmasın diye cevap her zaman aynıdır.
        /// </summary>
        [HttpPost("forgot-password")]
        [EnableRateLimiting("sifre-sifirlama")]
        public async Task<IActionResult> ForgotPassword([FromBody] SifremiUnuttumRequest request)
        {
            const string genel = "E-posta kayıtlıysa şifre sıfırlama bağlantısı gönderildi.";
            var email = request.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || HesapKurallari.EmailHata(email) != null)
                return Ok(new { message = genel });

            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email.ToLower() == email && x.Aktif);
            if (k != null)
            {
                try
                {
                    var token = SifreSifirlamaTokeni.Uret(_config, k.Id, k.SifreHash);
                    var site = (_config["Site:Url"] ?? "https://kage95.com").TrimEnd('/');
                    var link = $"{site}/?sifre-sifirla={Uri.EscapeDataString(token)}";
                    var ad = System.Net.WebUtility.HtmlEncode(k.KullaniciAdi);
                    var html = $"<p>Merhaba {ad},</p>" +
                        "<p>Kage 95 hesabın için şifre sıfırlama isteği aldık. Yeni şifre belirlemek için bağlantıya tıkla (30 dakika geçerli):</p>" +
                        $"<p><a href=\"{link}\">Şifremi sıfırla</a></p>" +
                        "<p>Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; şifren değişmez.</p>";
                    await _eposta.SendEmailAsync(k.Email, "Kage 95 - Şifre sıfırlama", html);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Şifre sıfırlama e-postası gönderilemedi (kullanıcı {Id}).", k.Id);
                }
            }
            return Ok(new { message = genel });
        }

        /// <summary>E-postadaki belirteçle yeni şifre belirler; eski oturumlar kapanır.</summary>
        [HttpPost("reset-password")]
        [EnableRateLimiting("sifre-sifirlama")]
        public async Task<IActionResult> ResetPassword([FromBody] SifreSifirlaTokenRequest request)
        {
            const string gecersiz = "Bağlantı geçersiz ya da süresi dolmuş. Yeniden şifre sıfırlama isteyin.";
            if (!SifreSifirlamaTokeni.Cozumle(request.Token, out var id)) return BadRequest(new { message = gecersiz });

            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Id == id && x.Aktif);
            if (k == null || !SifreSifirlamaTokeni.ImzaDogru(_config, request.Token, k.SifreHash))
                return BadRequest(new { message = gecersiz });

            var sifreHata = HesapKurallari.SifreHata(request.YeniSifre, k.KullaniciAdi);
            if (sifreHata != null) return BadRequest(new { message = sifreHata });

            k.SifreHash = BCrypt.Net.BCrypt.HashPassword(request.YeniSifre);
            k.TokenSurumu++;
            await _context.SaveChangesAsync();
            _sinirlayici.Sifirla(k.KullaniciAdi);
            return Ok(new { message = "Şifre güncellendi. Yeni şifrenle giriş yapabilirsin." });
        }

        /// <summary>
        /// Yeni kullanıcı kaydı
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("kayit")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.KullaniciAdi) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Sifre))
            {
                return BadRequest(new { message = "Kullanıcı adı, e-posta ve şifre zorunludur." });
            }

            if (!await _ayarlar.AcikMiAsync("kayitAcik", true))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yeni kayıtlar şu an kapalı." });
            }

            var kullaniciAdi = request.KullaniciAdi.Trim();
            var email = request.Email.Trim().ToLowerInvariant();

            var adHata = HesapKurallari.KullaniciAdiHata(kullaniciAdi);
            if (adHata != null) return BadRequest(new { message = adHata });
            var emailHata = HesapKurallari.EmailHata(email);
            if (emailHata != null) return BadRequest(new { message = emailHata });
            var sifreHata = HesapKurallari.SifreHata(request.Sifre, kullaniciAdi);
            if (sifreHata != null) return BadRequest(new { message = sifreHata });

            // Hesap varlığı sızmasın diye tek genel mesaj.
            var mevcut = await _context.Kullanicilar
                .AnyAsync(k => k.KullaniciAdi.ToLower() == kullaniciAdi.ToLowerInvariant() || k.Email.ToLower() == email);

            if (mevcut)
            {
                return BadRequest(new { message = "Kayıt oluşturulamadı. Bilgileri değiştirip tekrar deneyin." });
            }

            // Şifreyi BCrypt ile güvenli hash'le
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Sifre);

            var yeniKullanici = new Kullanici
            {
                KullaniciAdi = kullaniciAdi,
                Email = email,
                SifreHash = hashedPassword,
                Rol = "User",
                KayitTarihi = DateTime.UtcNow,
                SonGirisTarihi = DateTime.UtcNow
            };

            _context.Kullanicilar.Add(yeniKullanici);
            await _context.SaveChangesAsync();

            var token = _tokenService.GenerateToken(yeniKullanici);

            return Ok(new AuthResponse
            {
                Token = token,
                Id = yeniKullanici.Id,
                KullaniciAdi = yeniKullanici.KullaniciAdi,
                Email = yeniKullanici.Email,
                Rol = yeniKullanici.Rol,
                ProfilResmiURL = yeniKullanici.ProfilResmiURL,
                Yetkiler = Izinler.Cozumle(yeniKullanici)
            });
        }

        /// <summary>
        /// Kullanıcı girişi ve JWT token üretimi
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("giris")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.KullaniciAdiOrEmail) || string.IsNullOrWhiteSpace(request.Sifre))
            {
                return BadRequest(new { message = "Kullanıcı adı / e-posta ve şifre girilmelidir." });
            }

            var query = request.KullaniciAdiOrEmail.Trim().ToLowerInvariant();

            // Kilit IP + kullanıcı bazlı: yabancı biri gerçek hesabı kilitleyemesin.
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor";
            var sinirAnahtari = $"{ip}|{query}";

            var bekleme = _sinirlayici.Engelli(sinirAnahtari);
            if (bekleme != null)
            {
                var dk = Math.Max(1, (int)Math.Ceiling(bekleme.Value.TotalMinutes));
                Response.Headers["Retry-After"] = ((int)Math.Max(1, bekleme.Value.TotalSeconds)).ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = $"Çok fazla hatalı giriş denemesi. Lütfen yaklaşık {dk} dakika sonra tekrar deneyin." });
            }

            var kullanici = await _context.Kullanicilar
                .FirstOrDefaultAsync(k => k.KullaniciAdi.ToLower() == query || k.Email.ToLower() == query);

            if (kullanici == null)
            {
                // Timing Attack koruması: Kullanıcı sistemde bulunamadığında da sahte BCrypt doğrulaması
                // çalıştırılarak yanıt süresi eşitlenir ve kullanıcı adı tespiti açığı kapatılır.
                try { BCrypt.Net.BCrypt.Verify(request.Sifre, "$2a$11$e87wP4KPGqB3BsdG38d.wO8mO1fHw1lYk3fK4sD4H8K4sD4H8K4sD"); } catch { }
                _sinirlayici.HataKaydet(sinirAnahtari);
                return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı." });
            }

            // Parola doğrulaması: BCrypt. Eski kayıtlar (SHA256 hex) ilk başarılı girişte BCrypt'e çevrilir.
            // Düz metin karşılaştırması yapılmaz.
            var sifreDogru = HesapKurallari.SifreDogru(kullanici, request.Sifre, out var eskiHash);
            if (sifreDogru && eskiHash)
            {
                kullanici.SifreHash = BCrypt.Net.BCrypt.HashPassword(request.Sifre);
            }

            if (!sifreDogru)
            {
                _sinirlayici.HataKaydet(sinirAnahtari);
                return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı." });
            }

            _sinirlayici.Sifirla(sinirAnahtari);

            if (await HesapDurumu.BanliMiAsync(_context, kullanici))
            {
                if (eskiHash) await _context.SaveChangesAsync();
                var mesaj = string.IsNullOrWhiteSpace(kullanici.BanNedeni) ? "Hesabınız kapatılmış." : "Hesabınız banlanmış.";
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = mesaj, banNedeni = kullanici.BanNedeni, banBitis = kullanici.BanBitis });
            }

            kullanici.SonGirisTarihi = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = _tokenService.GenerateToken(kullanici);

            return Ok(new AuthResponse
            {
                Token = token,
                Id = kullanici.Id,
                KullaniciAdi = kullanici.KullaniciAdi,
                Email = kullanici.Email,
                Rol = kullanici.Rol,
                ProfilResmiURL = kullanici.ProfilResmiURL,
                Yetkiler = Izinler.Cozumle(kullanici)
            });
        }

        /// <summary>
        /// Oturum açmış aktif kullanıcının bilgileri
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserProfileResponse>> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var kullanici = await _context.Kullanicilar.FindAsync(userId);
            if (kullanici == null)
            {
                return NotFound(new { message = "Kullanıcı bulunamadı." });
            }

            return Ok(new UserProfileResponse
            {
                Id = kullanici.Id,
                KullaniciAdi = kullanici.KullaniciAdi,
                Email = kullanici.Email,
                Rol = kullanici.Rol,
                KayitTarihi = kullanici.KayitTarihi,
                Yetkiler = Izinler.Cozumle(kullanici)
            });
        }
    }
}
