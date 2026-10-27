using System.Security.Claims;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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
        private readonly IHttpClientFactory _http;
        private readonly IDataProtector _oauthState;

        public AuthController(AppDbContext context, ITokenService tokenService, ISiteAyarServisi ayarlar, IGirisSinirlayici sinirlayici,
            IEmailService eposta, IConfiguration config, ILogger<AuthController> log,
            IHttpClientFactory http, IDataProtectionProvider protection)
        {
            _eposta = eposta;
            _config = config;
            _log = log;
            _context = context;
            _tokenService = tokenService;
            _ayarlar = ayarlar;
            _sinirlayici = sinirlayici;
            _http = http;
            _oauthState = protection.CreateProtector("Kage95.OAuth.State");
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
                    var link = $"{site}/login.html?sifre-sifirla={Uri.EscapeDataString(token)}";
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

            // Hangi alanın çakıştığını söyle; deneme sayısını "kayit" hız sınırı kısıyor.
            if (await _context.Kullanicilar.AnyAsync(k => k.KullaniciAdi.ToLower() == kullaniciAdi.ToLowerInvariant()))
            {
                return Conflict(new { message = "Bu kullanıcı adı alınmış. Başka bir ad dene.", alan = "kullaniciAdi" });
            }
            if (await _context.Kullanicilar.AnyAsync(k => k.Email.ToLower() == email))
            {
                return Conflict(new { message = "Bu e-posta adresiyle zaten bir hesap var. Giriş yap ya da şifreni sıfırla.", alan = "email" });
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

            // Doğrulama e-postası gönderilemese de kayıt geçerli; kullanıcı sonra yeniden isteyebilir.
            await DogrulamaEpostasiGonderAsync(yeniKullanici);

            var token = _tokenService.GenerateToken(yeniKullanici);

            return Ok(new AuthResponse
            {
                Token = token,
                Id = yeniKullanici.Id,
                KullaniciAdi = yeniKullanici.KullaniciAdi,
                Email = yeniKullanici.Email,
                Rol = yeniKullanici.Rol,
                ProfilResmiURL = yeniKullanici.ProfilResmiURL,
                Yetkiler = Izinler.Cozumle(yeniKullanici),
                EmailDogrulandi = yeniKullanici.EmailDogrulandi
            });
        }

        /// <summary>E-postadaki doğrulama linkiyle hesabı doğrulanmış işaretler. Giriş gerektirmez.</summary>
        [HttpPost("verify-email")]
        [EnableRateLimiting("sifre-sifirlama")]
        public async Task<IActionResult> VerifyEmail([FromBody] EpostaDogrulaRequest request)
        {
            const string gecersiz = "Doğrulama bağlantısı geçersiz ya da süresi dolmuş. Hesabından yeni bağlantı isteyebilirsin.";
            if (!EpostaDogrulamaTokeni.Cozumle(request.Token, out var id)) return BadRequest(new { message = gecersiz });

            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Id == id);
            if (k == null || !EpostaDogrulamaTokeni.ImzaDogru(_config, request.Token, k.Email))
                return BadRequest(new { message = gecersiz });

            if (!k.EmailDogrulandi)
            {
                k.EmailDogrulandi = true;
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "E-posta adresin doğrulandı.", id = k.Id });
        }

        /// <summary>Oturum açmış, doğrulanmamış kullanıcıya doğrulama e-postasını yeniden gönderir.</summary>
        [Authorize]
        [HttpPost("resend-verification")]
        [EnableRateLimiting("sifre-sifirlama")]
        public async Task<IActionResult> ResendVerification()
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Unauthorized();
            var k = await _context.Kullanicilar.FindAsync(userId);
            if (k == null) return NotFound(new { message = "Kullanıcı bulunamadı." });
            if (k.EmailDogrulandi) return Ok(new { message = "E-posta adresin zaten doğrulanmış.", dogrulandi = true });

            if (!await DogrulamaEpostasiGonderAsync(k))
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "E-posta şu an gönderilemedi. Biraz sonra tekrar dene." });
            return Ok(new { message = $"Doğrulama bağlantısı {k.Email} adresine gönderildi." });
        }

        private async Task<bool> DogrulamaEpostasiGonderAsync(Kullanici k)
        {
            try
            {
                var token = EpostaDogrulamaTokeni.Uret(_config, k.Id, k.Email);
                var site = (_config["Site:Url"] ?? "https://kage95.com").TrimEnd('/');
                var link = $"{site}/login.html?eposta-dogrula={Uri.EscapeDataString(token)}";
                var ad = System.Net.WebUtility.HtmlEncode(k.KullaniciAdi);
                var html = $"<p>Merhaba {ad},</p>" +
                    "<p>Kage 95'e hoş geldin! E-posta adresini doğrulamak için bağlantıya tıkla (3 gün geçerli):</p>" +
                    $"<p><a href=\"{link}\">E-postamı doğrula</a></p>" +
                    "<p>Bu hesabı sen oluşturmadıysan bu e-postayı yok sayabilirsin.</p>";
                await _eposta.SendEmailAsync(k.Email, "Kage 95 - E-posta doğrulama", html);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Doğrulama e-postası gönderilemedi (kullanıcı {Id}).", k.Id);
                return false;
            }
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
                Yetkiler = Izinler.Cozumle(kullanici),
                EmailDogrulandi = kullanici.EmailDogrulandi
            });
        }

        [AllowAnonymous]
        [HttpGet("providers")]
        public IActionResult Providers() => Ok(new
        {
            google = !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientId"]) &&
                     !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientSecret"]),
            discord = !string.IsNullOrWhiteSpace(_config["Authentication:Discord:ClientId"]) &&
                      !string.IsNullOrWhiteSpace(_config["Authentication:Discord:ClientSecret"])
        });

        [AllowAnonymous]
        [HttpGet("external/{provider}")]
        public IActionResult ExternalStart(string provider)
        {
            provider = provider.ToLowerInvariant();
            var clientId = _config[$"Authentication:{provider}:ClientId"];
            var clientSecret = _config[$"Authentication:{provider}:ClientSecret"];
            if (provider is not ("google" or "discord") || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return NotFound(new { message = "Bu giriş sağlayıcısı yapılandırılmamış." });

            var state = _oauthState.Protect($"{provider}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{Guid.NewGuid():N}");
            var callback = CallbackUrl(provider);
            var query = provider == "google"
                ? $"client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(callback)}&response_type=code&scope=openid%20email%20profile&state={Uri.EscapeDataString(state)}&access_type=online"
                : $"client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(callback)}&response_type=code&scope=identify%20email&state={Uri.EscapeDataString(state)}";
            var endpoint = provider == "google" ? "https://accounts.google.com/o/oauth2/v2/auth" : "https://discord.com/oauth2/authorize";
            return Redirect(endpoint + "?" + query);
        }

        [AllowAnonymous]
        [HttpGet("external/{provider}/callback")]
        public async Task<IActionResult> ExternalCallback(string provider, string? code, string? state, string? error)
        {
            var loginUrl = LoginUrl();
            if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                return Redirect(loginUrl + "#oauth_error=" + Uri.EscapeDataString("Sosyal giriş iptal edildi."));
            try
            {
                var stateValue = _oauthState.Unprotect(state);
                var parts = stateValue.Split('|');
                if (parts.Length != 3 || parts[0] != provider.ToLowerInvariant() ||
                    !long.TryParse(parts[1], out var issued) ||
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issued > 600)
                    throw new InvalidOperationException("Geçersiz veya süresi dolmuş OAuth state.");

                var profile = await GetExternalProfileAsync(provider.ToLowerInvariant(), code, CallbackUrl(provider));
                if (profile == null || string.IsNullOrWhiteSpace(profile.Email) || !profile.EmailVerified)
                    return Redirect(loginUrl + "#oauth_error=" + Uri.EscapeDataString("Sağlayıcı doğrulanmış bir e-posta döndürmedi."));

                var user = provider.Equals("google", StringComparison.OrdinalIgnoreCase)
                    ? await _context.Kullanicilar.FirstOrDefaultAsync(x => x.GoogleSubject == profile.Subject)
                    : await _context.Kullanicilar.FirstOrDefaultAsync(x => x.DiscordUserId == profile.Subject);

                if (user == null)
                {
                    var email = profile.Email.Trim().ToLowerInvariant();
                    if (await _context.Kullanicilar.AnyAsync(x => x.Email.ToLower() == email))
                        return Redirect(loginUrl + "#oauth_error=" + Uri.EscapeDataString("Bu e-posta zaten şifreli bir hesaba bağlı. Önce normal giriş yapın."));
                    if (!await _ayarlar.AcikMiAsync("kayitAcik", true))
                        return Redirect(loginUrl + "#oauth_error=" + Uri.EscapeDataString("Yeni kayıtlar şu an kapalı."));

                    user = new Kullanici
                    {
                        KullaniciAdi = await UniqueUsernameAsync(profile.Name),
                        Email = email,
                        SifreHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))),
                        Rol = "User",
                        ProfilResmiURL = profile.Picture,
                        EmailDogrulandi = true, // sağlayıcı e-postayı doğrulamış
                        KayitTarihi = DateTime.UtcNow,
                        SonGirisTarihi = DateTime.UtcNow
                    };
                    if (provider == "google") user.GoogleSubject = profile.Subject;
                    else user.DiscordUserId = profile.Subject;
                    _context.Kullanicilar.Add(user);
                }
                else
                {
                    user.SonGirisTarihi = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(profile.Picture)) user.ProfilResmiURL = profile.Picture;
                }

                if (await HesapDurumu.BanliMiAsync(_context, user))
                    return Redirect(loginUrl + "#oauth_error=" + Uri.EscapeDataString("Bu hesap kapatılmış veya banlanmış."));
                await _context.SaveChangesAsync();
                var token = _tokenService.GenerateToken(user);
                return Redirect(loginUrl + "#oauth_token=" + Uri.EscapeDataString(token));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "OAuth callback başarısız ({Provider}).", provider);
                return Redirect(loginUrl + "#oauth_error=" + Uri.EscapeDataString("Sosyal giriş tamamlanamadı."));
            }
        }

        private string CallbackUrl(string provider) =>
            $"{(_config["Site:Url"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/')}/api/auth/external/{provider}/callback";

        private string LoginUrl() => $"{(_config["Site:Url"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/')}/login.html";

        private async Task<string> UniqueUsernameAsync(string? displayName)
        {
            var baseName = new string((displayName ?? "KageOkuru").Where(char.IsLetterOrDigit).ToArray());
            if (baseName.Length < 3) baseName = "KageOkuru";
            baseName = baseName[..Math.Min(35, baseName.Length)];
            var candidate = baseName;
            for (var i = 0; await _context.Kullanicilar.AnyAsync(x => x.KullaniciAdi == candidate); i++)
                candidate = $"{baseName[..Math.Min(35, baseName.Length)]}{Random.Shared.Next(1000, 9999)}";
            return candidate;
        }

        private async Task<ExternalProfile?> GetExternalProfileAsync(string provider, string code, string callback)
        {
            var client = _http.CreateClient();
            if (provider == "google")
            {
                var token = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code, ["client_id"] = _config["Authentication:Google:ClientId"]!, ["client_secret"] = _config["Authentication:Google:ClientSecret"]!,
                    ["redirect_uri"] = callback, ["grant_type"] = "authorization_code"
                }));
                token.EnsureSuccessStatusCode();
                var tokenData = await token.Content.ReadFromJsonAsync<GoogleToken>();
                var response = await client.GetAsync($"https://openidconnect.googleapis.com/v1/userinfo?access_token={Uri.EscapeDataString(tokenData?.AccessToken ?? "")}");
                response.EnsureSuccessStatusCode();
                var p = await response.Content.ReadFromJsonAsync<GoogleProfile>();
                return p == null ? null : new ExternalProfile(p.Sub, p.Name, p.Email, p.EmailVerified, p.Picture);
            }
            var discordToken = await client.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code, ["client_id"] = _config["Authentication:Discord:ClientId"]!, ["client_secret"] = _config["Authentication:Discord:ClientSecret"]!,
                ["redirect_uri"] = callback, ["grant_type"] = "authorization_code"
            }));
            discordToken.EnsureSuccessStatusCode();
            var discordData = await discordToken.Content.ReadFromJsonAsync<DiscordToken>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordData?.AccessToken);
            var d = await client.GetFromJsonAsync<DiscordProfile>("https://discord.com/api/users/@me");
            return d == null ? null : new ExternalProfile(d.Id, d.Username, d.Email ?? "", d.Verified, string.IsNullOrWhiteSpace(d.Avatar) ? null : $"https://cdn.discordapp.com/avatars/{d.Id}/{d.Avatar}.png");
        }

        private sealed record ExternalProfile(string Subject, string Name, string Email, bool EmailVerified, string? Picture);
        private sealed class GoogleToken { [JsonPropertyName("access_token")] public string? AccessToken { get; set; } }
        private sealed class GoogleProfile { public string Sub { get; set; } = ""; public string Name { get; set; } = ""; public string Email { get; set; } = ""; [JsonPropertyName("email_verified")] public bool EmailVerified { get; set; } public string? Picture { get; set; } }
        private sealed class DiscordToken { [JsonPropertyName("access_token")] public string? AccessToken { get; set; } }
        private sealed class DiscordProfile { public string Id { get; set; } = ""; public string Username { get; set; } = ""; public string? Email { get; set; } public bool Verified { get; set; } public string? Avatar { get; set; } }

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
                Yetkiler = Izinler.Cozumle(kullanici),
                EmailDogrulandi = kullanici.EmailDogrulandi
            });
        }
    }
}
