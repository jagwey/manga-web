using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Kage95.Backend.Data;
using Kage95.Backend.Services;
using Kage95.Backend.Security;
using Kage95.Backend.Hubs;

var builder = WebApplication.CreateBuilder(args);
// Gizli ayarlar (JWT anahtarı) git'e girmeyen yerel dosyadan ya da ortam değişkeninden okunur.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 1. Veritabanı (SQL Server - Kage95DB)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=.\\SQLEXPRESS;Database=Kage95DB;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Servisler
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IDenetimServisi, DenetimServisi>();
builder.Services.AddScoped<ISiteAyarServisi, SiteAyarServisi>();
builder.Services.AddSingleton<IGirisSinirlayici, GirisSinirlayici>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TercihServisi>();
builder.Services.AddScoped<YorumServisi>();
builder.Services.AddSingleton<IslemSinirlayici>();
builder.Services.AddHostedService<SohbetTemizlikServisi>();
builder.Services.AddHostedService<MangaCopTemizlikServisi>();
builder.Services.AddSignalR(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment())
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 3. JWT Kimlik Doğrulama
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"];
if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:Secret ayarlanmamış ya da çok kısa (en az 32 karakter). " +
        "backend/appsettings.Local.json dosyasına {\"JwtSettings\":{\"Secret\":\"<uzun rastgele metin>\"}} yazın " +
        "ya da JwtSettings__Secret ortam değişkenini tanımlayın.");
}
var issuer = jwtSettings["Issuer"] ?? "Kage95API";
var audience = jwtSettings["Audience"] ?? "Kage95Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    // Her korumalı istekte kullanıcıyı DB'den doğrula: silinmiş, banlı ya da oturumları kapatılmış
    // (tv claim'i TokenSurumu ile uyuşmayan) kullanıcı anında 401 alır.
    options.Events = new JwtBearerEvents
    {
        // SignalR (WebSocket) başlık gönderemez: belirteç /hub/... yollarında access_token sorgu parametresinden alınır.
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hub"))
                ctx.Token = token;
            return Task.CompletedTask;
        },
        OnTokenValidated = async ctx =>
        {
            var idStr = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idStr, out var id)) { ctx.Fail("Geçersiz oturum."); return; }

            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var k = await db.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ctx.HttpContext.RequestAborted);
            if (k == null) { ctx.Fail("Kullanıcı bulunamadı."); return; }

            if (await HesapDurumu.BanliMiAsync(db, k, ctx.HttpContext.RequestAborted)) { ctx.Fail("Hesap banlı."); return; }

            var tvStr = ctx.Principal?.FindFirst("tv")?.Value;
            var tv = int.TryParse(tvStr, out var t) ? t : 0;
            if (tv != k.TokenSurumu) ctx.Fail("Oturum sona erdi.");
        }
    };
});

// 4. CORS: yayında yalnızca Cors:Origins içindeki adreslerden gelen isteklere izin verilir.
// Geliştirmede (Development) tüm kaynaklara açıktır; böylece dosya ya da yerel sunucudan açılan sayfalar çalışır.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Site", policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("X-Total-Count");
        else if (corsOrigins.Length > 0)
            policy.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("X-Total-Count");
        // Origin tanımlı değilse hiçbir dış kaynağa izin verilmez.
    });
});

// 4b. IP başına hız sınırı (kullanıcı adı bazlı kilide ek olarak; sprey ve toplu kayıt denemelerini yavaşlatır).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor";
    o.AddPolicy("giris", c => RateLimitPartition.GetFixedWindowLimiter(Ip(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(5) }));
    o.AddPolicy("kayit", c => RateLimitPartition.GetFixedWindowLimiter(Ip(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(30) }));
});

// 5. Controller ve JSON Ayarları
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 6. Swagger / OpenAPI (JWT Destekli)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Şema eki: öne çıkan görsel kolonu yoksa eklenir (idempotent; migration kullanılmıyor)
using (var kapsam = app.Services.CreateScope())
{
    try
    {
        kapsam.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRaw(
            "IF COL_LENGTH(N'dbo.Mangalar', N'OneCikanResimURL') IS NULL ALTER TABLE dbo.Mangalar ADD OneCikanResimURL NVARCHAR(500) NULL;" +
            "IF COL_LENGTH(N'dbo.Mangalar', N'SilinmeTarihi') IS NULL ALTER TABLE dbo.Mangalar ADD SilinmeTarihi DATETIME2 NULL;" +
            "IF OBJECT_ID(N'dbo.KullaniciTercihleri') IS NOT NULL AND COL_LENGTH(N'dbo.KullaniciTercihleri', N'AdEfekti') IS NULL ALTER TABLE dbo.KullaniciTercihleri ADD AdEfekti NVARCHAR(20) NULL;" +
            // Eski ayrı öne çıkanlar tablosu kaldırıldı; öne çıkanlar artık mangadaki OneCikan bayrağından gelir.
            "IF OBJECT_ID(N'dbo.OneCikanlar', N'U') IS NOT NULL DROP TABLE dbo.OneCikanlar;" +
            // Forum tabloları
            "IF OBJECT_ID(N'dbo.ForumKonulari', N'U') IS NULL BEGIN CREATE TABLE dbo.ForumKonulari (Id INT IDENTITY(1,1) PRIMARY KEY, Kategori VARCHAR(20) NOT NULL, KullaniciId INT NOT NULL, Baslik NVARCHAR(120) NOT NULL, Icerik NVARCHAR(4000) NOT NULL, MangaId INT NULL, BolumNo DECIMAL(6,2) NULL, Spoiler BIT NOT NULL DEFAULT 0, Sabit BIT NOT NULL DEFAULT 0, Kilitli BIT NOT NULL DEFAULT 0, Silindi BIT NOT NULL DEFAULT 0, Tarih DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), SonYanitTarihi DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), YanitSayisi INT NOT NULL DEFAULT 0, BegeniSayisi INT NOT NULL DEFAULT 0); CREATE INDEX IX_ForumKonulari_Kategori_SonYanit ON dbo.ForumKonulari (Kategori, SonYanitTarihi); CREATE INDEX IX_ForumKonulari_MangaId ON dbo.ForumKonulari (MangaId); END;" +
            "IF OBJECT_ID(N'dbo.ForumYanitlari', N'U') IS NULL BEGIN CREATE TABLE dbo.ForumYanitlari (Id INT IDENTITY(1,1) PRIMARY KEY, KonuId INT NOT NULL, KullaniciId INT NOT NULL, Icerik NVARCHAR(4000) NOT NULL, Spoiler BIT NOT NULL DEFAULT 0, Silindi BIT NOT NULL DEFAULT 0, Tarih DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()); CREATE INDEX IX_ForumYanitlari_KonuId_Tarih ON dbo.ForumYanitlari (KonuId, Tarih); END;" +
            "IF OBJECT_ID(N'dbo.ForumBegenileri', N'U') IS NULL BEGIN CREATE TABLE dbo.ForumBegenileri (Id INT IDENTITY(1,1) PRIMARY KEY, KonuId INT NOT NULL, KullaniciId INT NOT NULL); CREATE UNIQUE INDEX UX_ForumBegenileri ON dbo.ForumBegenileri (KonuId, KullaniciId); END;" +
            "UPDATE dbo.Mangalar SET OneCikan = 1, KapakResmiURL = '/uploads/vinland_saga_cover.jpg', OneCikanResimURL = '/uploads/vinland_saga_banner.jpg' WHERE MangaAdi = 'Vinland Saga';");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "OneCikanResimURL kolonu eklenemedi");
    }
}

// Forum karşılama konusu: yoksa site sahibi adına bir kez, sabit ve kilitli olarak açılır.
using (var kapsam = app.Services.CreateScope())
{
    try
    {
        var db = kapsam.ServiceProvider.GetRequiredService<AppDbContext>();
        const string karsilamaBaslik = "Gölge Meclisi'ne hoş geldin: Kage 95 nedir, neden var?";
        if (!db.ForumKonulari.Any(k => k.Baslik == karsilamaBaslik))
        {
            var sahip = db.Kullanicilar.Where(k => k.Rol == Roller.Owner).OrderBy(k => k.Id).FirstOrDefault()
                ?? db.Kullanicilar.Where(k => k.Rol == Roller.Admin).OrderBy(k => k.Id).FirstOrDefault();
            if (sahip != null)
            {
                var simdi = DateTime.UtcNow;
                db.ForumKonulari.Add(new Kage95.Backend.Models.ForumKonu
                {
                    Kategori = "duyuru",
                    KullaniciId = sahip.Id,
                    Baslik = karsilamaBaslik,
                    Icerik = """
Selam gölge. Kage 95'e, sitenin kalbi olacak bu foruma hoş geldin. Kim olduğumuzu, neden kurulduğumuzu ve burada neler yapabileceğini tek yerde topladık.

**Neden kurulduk?**
Manga okumak bir zevk olmalı, işkence değil. Her sayfada açılan pencereler, ekranı kaplayan reklamlar, okurken yüklenmeyen sayfalar, seni başka siteye fırlatan sahte "devam et" düğmeleri... Hepimiz yaşadık. Kage 95 tam olarak buna bir cevap olarak doğdu: sade, hızlı ve okura saygı duyan bir okuma evi.

**Felsefemiz**
• *Önce okur.* Sitede alınan her karar "bu okurun işine yarıyor mu?" sorusuyla başlar.
• *Rahatsız edici reklam yok.* Pop-up yok, yönlendirme yok, sayfanın ortasına yapışan afiş yok. Okurken sadece sen ve hikaye varsınız.
• *Sade ama güçlü.* Kırmızı-siyah, gölgeli, dikkat dağıtmayan bir tasarım. Gereksiz süs değil, işe yarayan özellik.
• *Topluluk bizim omurgamız.* Kage 95 sadece bir okuma sitesi değil, aynı seriyi seven insanların buluştuğu bir yer.

**Sitede neler var?**
• **Kütüphane:** Okuduğun serileri takip et, kaldığın bölümden devam et.
• **Hızlı okuyucu:** Bölümler hızlı açılır, mobilde de rahat okunur.
• **Puan, seviye ve rütbe:** Okudukça, yorum yaptıkça puan kazan, rütbeni yükselt, liderlik tablosuna gir.
• **Ad stilleri:** Kazandığın puanlarla adına renk, efekt ve unvan ekle. Adın forumda, sohbette ve yorumlarda öyle görünür.
• **Canlı sohbet:** Diğer okurlarla anlık konuş.
• **Yorumlar ve bildirimler:** Serilere yorum yaz, sana yanıt gelince haberin olsun.
• **Uygulama gibi kur:** Kage 95'i telefonuna ya da bilgisayarına uygulama olarak ekleyebilirsin.

**Bu forum neden farklı?**
Burada konunu bir seriye ve bölüme bağlayabilirsin. Mesela "Bölüm 120" diye işaretlediğin bir teori, o bölüme henüz gelmemiş okurlara bulanık görünür. O bölümü okumuş olanlar ise direkt görür. Yani spoiler korkusu olmadan, rahatça tartışabilirsin. Teoriler kategorisi her zaman spoiler sayılır.

**Kategoriler**
告 Duyurular: Sitedeki yenilikler ve ekipten haberler.
道 Dojo: Genel sohbet, aklına ne gelirse.
縁 Tanışma: Kendini tanıt, en sevdiğin seriyi söyle.
巻 Seri Sohbeti: Bir serinin geneli hakkında konuş.
話 Bölüm Tartışması: Yeni çıkan bölümleri tartış.
謎 Teoriler: Tahminlerin, analizlerin, çılgın fikirlerin.
薦 Öneri: "Buna benzer ne okuyayım?" sorusunun cevabı.
声 Site Sesi: Hata bildirimi, istek ve öneri. Seni dinliyoruz.

**Kısa kurallar**
1. Saygılı ol. Hakaret, nefret söylemi ve kişisel saldırı yok.
2. Spoiler varsa işaretle ya da konuyu bölüme bağla.
3. Reklam, spam ve başka sitelere yönlendirme yok.
4. Konuyu doğru kategoride aç.
Kurallara uymayan içerik uyarısız silinebilir.

Kage 95 sizinle büyüyor. Bir fikrin, bir şikayetin ya da sadece bir "merhaba"n varsa Site Sesi'nde ya da Tanışma'da yaz. İyi okumalar, gölgede görüşürüz. 影
""",
                    Sabit = true,
                    Kilitli = true,
                    Tarih = simdi,
                    SonYanitTarihi = simdi
                });
                db.SaveChanges();
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Forum karşılama konusu açılamadı");
    }
}

// 7. Middleware Boru Hattı
var fho = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Yalnızca Cloudflare uçlarından gelen X-Forwarded-* başlıklarına güvenilir (sahte IP ile hız sınırı aşılamaz).
fho.KnownIPNetworks.Clear();
fho.KnownProxies.Clear();
foreach (var ag in new[]
{
    "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18",
    "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17",
    "162.158.0.0/15", "104.16.0.0/13", "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
    "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32",
    "2a06:98c0::/29", "2c0f:f248::/32"
})
    fho.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(ag));
app.UseForwardedHeaders(fho);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kage 95 Manga API v1");
        c.RoutePrefix = "swagger"; // /swagger adresinde açılır
    });
}
else
{
    // Yayında hata ayrıntısı (yığın izi, dosya yolu) istemciye gitmez.
    app.UseExceptionHandler(h => h.Run(async ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsJsonAsync(new { message = "Beklenmeyen bir hata oluştu." });
    }));
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Tarayıcıya yönelik temel güvenlik başlıkları
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

// Otomatik veritabanı bağlantı kontrolü ve log
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (db.Database.CanConnect())
        {
            Console.WriteLine("[Kage 95 API] SQL Server 'Kage95DB' veritabanına başarıyla bağlanıldı.");
        }
        else
        {
            Console.WriteLine("[Kage 95 API UYARI] SQL Server 'Kage95DB' veritabanına ulaşılamadı.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Kage 95 API HATA] DB Bağlantı hatası: {ex.Message}");
    }
}

app.UseCors("Site");
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // Sürümlü (?v=) dosyalar uzun süre, diğer statikler kısa süre önbelleğe alınır; html/sw hep doğrulanır.
    OnPrepareResponse = ctx =>
    {
        var yol = ctx.File.Name.ToLowerInvariant();
        var h = ctx.Context.Response.Headers;
        if (yol.EndsWith(".html") || yol == "sw.js" || yol.EndsWith(".webmanifest"))
            h.CacheControl = "no-cache";
        else if (ctx.Context.Request.Query.ContainsKey("v"))
            h.CacheControl = "public, max-age=31536000, immutable";
        else
            h.CacheControl = "public, max-age=3600";
    }
});

app.UseAuthentication();
app.UseAuthorization();

// Bakım modu: aktifken yönetici/site sahibi dışındaki yazma istekleri (giriş hariç) 503 döner.
app.Use(async (ctx, next) =>
{
    var m = ctx.Request.Method;
    if (!HttpMethods.IsGet(m) && !HttpMethods.IsHead(m) && !HttpMethods.IsOptions(m)
        && ctx.Request.Path.StartsWithSegments("/api")
        && !ctx.Request.Path.StartsWithSegments("/api/auth/login")
        && !ctx.Request.Path.StartsWithSegments("/api/admin"))
    {
        var ayarlar = ctx.RequestServices.GetRequiredService<ISiteAyarServisi>();
        if (await ayarlar.AcikMiAsync("bakimModu", false))
        {
            var rol = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!string.Equals(rol, "Owner", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await ctx.Response.WriteAsJsonAsync(new { message = "Site şu an bakım modundadır. Lütfen daha sonra tekrar deneyin." });
                return;
            }
        }
    }
    await next();
});

app.MapControllers();
app.MapHub<SohbetHub>(SohbetHub.Yol);

// Basit sağlık/durum rotası (kök '/' ise UseDefaultFiles + UseStaticFiles üzerinden wwwroot/index.html sunar)
app.MapGet("/api/durum", () => Results.Ok(new
{
    api = "Kage 95 Manga API",
    status = "Active",
    version = "2.0.0"
}));

app.Run();
