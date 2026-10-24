using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;

namespace Kage95.Backend.Security
{
    /// <summary>
    /// Veritabanından o anki kullanıcıyı okuyup Aktif ve (Owner veya listelenen izinlerden herhangi biri)
    /// kontrolü yapar. Token'daki rol claim'ine güvenilmez; izin değişikliği hemen etkili olur.
    /// Kullanım: [Yetki("manga.yaz")] veya [Yetki("manga.yaz", "bolum.yaz")] (herhangi biri yeterli).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class YetkiAttribute : TypeFilterAttribute
    {
        public YetkiAttribute(params string[] anahtarlar) : base(typeof(YetkiFiltresi))
        {
            Arguments = new object[] { anahtarlar };
        }
    }

    public class YetkiFiltresi : IAsyncAuthorizationFilter
    {
        private readonly AppDbContext _context;
        private readonly string[] _anahtarlar;

        public YetkiFiltresi(AppDbContext context, string[] anahtarlar)
        {
            _context = context;
            _anahtarlar = anahtarlar;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            var idStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (user.Identity?.IsAuthenticated != true || !int.TryParse(idStr, out var id))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Bu işlem için giriş yapmalısınız." });
                return;
            }

            var kullanici = await _context.Kullanicilar.AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == id, context.HttpContext.RequestAborted);
            if (kullanici == null)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Oturumunuz geçersiz." });
                return;
            }
            if (await HesapDurumu.BanliMiAsync(_context, kullanici, context.HttpContext.RequestAborted))
            {
                context.Result = new ObjectResult(new { message = "Hesabınız kapatılmış." }) { StatusCode = 403 };
                return;
            }

            var sahip = Izinler.Cozumle(kullanici);
            if (!_anahtarlar.Any(sahip.Contains))
            {
                context.Result = new ObjectResult(new { message = "Bu işlem için yetkiniz yok." }) { StatusCode = 403 };
                return;
            }

            context.HttpContext.Items["Kullanici"] = kullanici;
        }
    }
}
