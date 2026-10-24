using System.Text;
using System.Threading.RateLimiting;
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

// 7. Middleware Boru Hattı
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
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SohbetHub>(SohbetHub.Yol);

// Basit kök karşılama rotası
app.MapGet("/", () => Results.Ok(new
{
    api = "Kage 95 Manga API",
    status = "Active",
    version = "1.0.0"
}));

app.Run();
