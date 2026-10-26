using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Controllers;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Kage95.Backend.Services;

namespace Kage95.Backend.Hubs
{
    /// <summary>Sohbet mesajı yayın/REST şekli. Metin düz metindir (HTML kodlaması yok; istemci textContent ile basar).</summary>
    public class SohbetMesajDto
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string? ProfilResmiURL { get; set; }
        public string Rutbe { get; set; } = string.Empty;
        public int Seviye { get; set; }
        public string Metin { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StickerId { get; set; }
        public DateTime Tarih { get; set; }
        public AdStiliDto? AdStili { get; set; }
    }

    public static class SohbetMetni
    {
        public const int MaksUzunluk = 300;

        /// <summary>Kontrol/biçim (sıfır genişlikli, yön) karakterlerini atar; boşluk/satır zincirlerini tek boşluğa indirir; kırpar.</summary>
        public static string Temizle(string? ham)
        {
            if (string.IsNullOrEmpty(ham)) return string.Empty;
            var sb = new StringBuilder(Math.Min(ham.Length, 1000));
            var oncekiBosluk = false;
            foreach (var c in ham)
            {
                var kat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (char.IsWhiteSpace(c) || kat == UnicodeCategory.SpaceSeparator)
                {
                    if (!oncekiBosluk && sb.Length > 0) sb.Append(' ');
                    oncekiBosluk = true;
                    continue;
                }
                if (char.IsControl(c) || kat == UnicodeCategory.Format || kat == UnicodeCategory.LineSeparator ||
                    kat == UnicodeCategory.ParagraphSeparator || kat == UnicodeCategory.PrivateUse || kat == UnicodeCategory.OtherNotAssigned)
                    continue;
                sb.Append(c);
                oncekiBosluk = false;
            }
            return sb.ToString().Trim();
        }

        public static DateTime Utc(DateTime t) => DateTime.SpecifyKind(t, DateTimeKind.Utc);
    }

    public class SohbetHub : Hub
    {
        public const string Yol = "/hub/sohbet";
        private static int _cevrimici;
        private static readonly ConcurrentDictionary<int, string> SonMetin = new();
        public static int Cevrimici => Volatile.Read(ref _cevrimici);

        private readonly AppDbContext _db;
        private readonly ISiteAyarServisi _ayarlar;
        private readonly TercihServisi _tercihler;
        private readonly IslemSinirlayici _sinir;

        public SohbetHub(AppDbContext db, ISiteAyarServisi ayarlar, TercihServisi tercihler, IslemSinirlayici sinir)
        {
            _db = db;
            _ayarlar = ayarlar;
            _tercihler = tercihler;
            _sinir = sinir;
        }

        private static int _sonYayinlananCevrimici = -1;
        private static long _sonYayinZamaniTicks = 0;
        private static readonly SemaphoreSlim _yayinSemaphoru = new(1, 1);

        public override async Task OnConnectedAsync()
        {
            // WebSocket DoS ve bağlantı seli engeli: IP başına dakikada en fazla 30 bağlantı
            var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor";
            if (!_sinir.Dene($"hubconn:{ip}", 30, TimeSpan.FromMinutes(1)))
            {
                Context.Abort();
                return;
            }

            var n = Interlocked.Increment(ref _cevrimici);
            await base.OnConnectedAsync();
            // Yeni bağlanan kullanıcıya güncel sayıyı hemen ilet
            await Clients.Caller.SendAsync("Cevrimici", new { sayi = n });
            // Diğer tüm kullanıcılara yayını sınırla (broadcast storm koruması)
            await KorumaliCevrimiciYayiniAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var n = Interlocked.Decrement(ref _cevrimici);
            if (n < 0) { Interlocked.Exchange(ref _cevrimici, 0); n = 0; }
            await base.OnDisconnectedAsync(exception);
            await KorumaliCevrimiciYayiniAsync();
        }

        private async Task KorumaliCevrimiciYayiniAsync()
        {
            var suanki = Cevrimici;
            var simdiTicks = DateTime.UtcNow.Ticks;
            var oncekiTicks = Volatile.Read(ref _sonYayinZamaniTicks);
            var gecenSure = TimeSpan.FromTicks(simdiTicks - oncekiTicks);

            // Son yayından bu yana 5 saniye geçmemişse ve fark 5'ten azsa yayını atla
            if (gecenSure < TimeSpan.FromSeconds(5) && Math.Abs(suanki - _sonYayinlananCevrimici) < 5)
                return;

            if (!await _yayinSemaphoru.WaitAsync(0))
                return;

            try
            {
                _sonYayinlananCevrimici = suanki;
                Volatile.Write(ref _sonYayinZamaniTicks, DateTime.UtcNow.Ticks);
                await Clients.All.SendAsync("Cevrimici", new { sayi = suanki });
            }
            finally
            {
                _yayinSemaphoru.Release();
            }
        }

        /// <summary>Genel odaya mesaj gönderir. Yalnızca giriş yapmış kullanıcı; sticker için seviye kapısı vardır.</summary>
        [Authorize]
        public async Task GonderAsync(string? metin, string? stickerId = null)
        {
            var ct = Context.ConnectionAborted;
            if (!int.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid))
                throw new HubException("Mesaj göndermek için giriş yapmalısınız.");

            var k = await _db.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
            if (k == null) throw new HubException("Oturumunuz geçersiz.");
            if (await HesapDurumu.BanliMiAsync(_db, k, ct)) throw new HubException("Hesabınız kapatılmış.");
            var tv = int.TryParse(Context.User?.FindFirst("tv")?.Value, out var t) ? t : 0;
            if (tv != k.TokenSurumu) throw new HubException("Oturumunuz sona erdi, lütfen yeniden giriş yapın.");

            if (!await _ayarlar.AcikMiAsync("sohbetAcik", true))
                throw new HubException("Sohbet şu an kapalı.");
            if (await _ayarlar.AcikMiAsync("bakimModu", false) && !Izinler.OwnerMi(k) && !Izinler.AdminMi(k))
                throw new HubException("Site şu an bakım modundadır.");

            var sus = await _db.SohbetSusturmalari.AsNoTracking().FirstOrDefaultAsync(s => s.KullaniciId == uid, ct);
            if (sus != null && sus.Bitis > DateTime.UtcNow)
            {
                var dk = Math.Max(1, (int)Math.Ceiling((sus.Bitis - DateTime.UtcNow).TotalMinutes));
                throw new HubException($"Sohbette susturuldunuz. Yaklaşık {dk} dakika sonra tekrar yazabilirsiniz.");
            }

            var ham = metin ?? string.Empty;
            if (ham.Length > 2000) throw new HubException($"Mesaj en fazla {SohbetMetni.MaksUzunluk} karakter olabilir.");
            var temiz = SohbetMetni.Temizle(ham);
            if (temiz.Length > SohbetMetni.MaksUzunluk) throw new HubException($"Mesaj en fazla {SohbetMetni.MaksUzunluk} karakter olabilir.");

            string? sticker = null;
            if (!string.IsNullOrWhiteSpace(stickerId))
            {
                var st = OdulKatalogu.Sticker(stickerId.Trim());
                if (st == null) throw new HubException("Bilinmeyen sticker.");
                sticker = st.Value.Sticker.Id;
            }
            if (temiz.Length == 0 && sticker == null) throw new HubException("Mesaj boş olamaz.");

            var toplam = await _tercihler.ToplamPuanAsync(uid);
            var seviye = PuanServisi.Seviye(toplam);
            if (sticker != null)
            {
                var paket = OdulKatalogu.Sticker(sticker)!.Value.Paket;
                if (seviye < paket.Seviye) throw new HubException($"'{paket.Ad}' sticker paketi için seviye {paket.Seviye} gerekir.");
            }

            if (temiz.Length > 0 && SonMetin.TryGetValue(uid, out var onceki) && string.Equals(onceki, temiz, StringComparison.Ordinal))
                throw new HubException("Aynı mesajı art arda gönderemezsiniz.");
            if (!_sinir.Dene("sohbet2s:" + uid, 1, TimeSpan.FromSeconds(2)))
                throw new HubException("Çok hızlı yazıyorsunuz. Lütfen biraz bekleyin.");
            if (!_sinir.Dene("sohbet1m:" + uid, 15, TimeSpan.FromMinutes(1)))
                throw new HubException("Dakikada en fazla 15 mesaj gönderebilirsiniz.");
            if (sticker != null && !_sinir.Dene("sohbetst:" + uid, 1, TimeSpan.FromSeconds(3)))
                throw new HubException("Sticker'ları en fazla 3 saniyede bir gönderebilirsiniz.");

            var kayit = new SohbetMesaji { KullaniciId = uid, Metin = temiz, StickerId = sticker, Tarih = DateTime.UtcNow };
            _db.SohbetMesajlari.Add(kayit);
            await _db.SaveChangesAsync(ct);
            if (temiz.Length > 0)
            {
                if (SonMetin.Count > 500) SonMetin.Clear();
                SonMetin[uid] = temiz;
            }

// Canli sohbette anlik DELETE lock kaldirildi; temizlik arka planda SohbetTemizlikServisi ile yapilir

            var stil = await _tercihler.TekAsync(uid);
            await Clients.All.SendAsync("MesajGeldi", new SohbetMesajDto
            {
                Id = kayit.Id,
                KullaniciId = uid,
                KullaniciAdi = k.KullaniciAdi,
                ProfilResmiURL = k.ProfilResmiURL,
                Rutbe = PuanServisi.RutbeSeviyeden(seviye),
                Seviye = seviye,
                Metin = temiz,
                StickerId = sticker,
                Tarih = SohbetMetni.Utc(kayit.Tarih),
                AdStili = stil
            });

            if (temiz.Contains('@'))
            {
                var eslesmeler = Regex.Matches(temiz, @"@([\p{L}\p{Nd}_.\-]{3,30})");
                var adlar = eslesmeler.Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
                if (adlar.Count > 0)
                {
                    var etiketlenenIdler = await _db.Kullanicilar.AsNoTracking()
                        .Where(x => x.Aktif && x.Id != uid && adlar.Contains(x.KullaniciAdi))
                        .Select(x => x.Id)
                        .ToListAsync(ct);
                    if (etiketlenenIdler.Count > 0)
                    {
                        await BildirimController.BildirimKaydetAsync(
                            _db,
                            etiketlenenIdler,
                            "etiket",
                            $"{k.KullaniciAdi} sohbette senden bahsetti",
                            "#/sohbet");
                    }
                }
            }
        }
    }

    /// <summary>Veritabanı şişmesini önlemek için periyodik temizlik servisi (sohbet, bildirim, geçici puan sayaçları, şikayet ve denetim kayıtları).</summary>
    public class SohbetTemizlikServisi : BackgroundService
    {
        private readonly IServiceScopeFactory _fabrika;
        public SohbetTemizlikServisi(IServiceScopeFactory fabrika) { _fabrika = fabrika; }

        protected override async Task ExecuteAsync(CancellationToken durdur)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), durdur); } catch (OperationCanceledException) { return; }
            var sonDerinTemizlik = DateTime.UtcNow;
            while (!durdur.IsCancellationRequested)
            {
                try
                {
                    using var scope = _fabrika.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var simdi = DateTime.UtcNow;

                    // 1. Sohbette en yeni 50 mesaj dışındakileri ve silinmişleri temizle (Her 3 dakikada bir, kilit oluşturmadan)
                    var ellinciSohbetId = await db.SohbetMesajlari.AsNoTracking()
                        .Where(m => !m.Silindi)
                        .OrderByDescending(m => m.Id)
                        .Skip(50 - 1)
                        .Select(m => (int?)m.Id)
                        .FirstOrDefaultAsync(durdur);
                    if (ellinciSohbetId.HasValue)
                        await db.SohbetMesajlari.Where(m => m.Id < ellinciSohbetId.Value || m.Silindi).ExecuteDeleteAsync(durdur);
                    else
                        await db.SohbetMesajlari.Where(m => m.Silindi).ExecuteDeleteAsync(durdur);

                    // Diğer derin temizlikler her 6 saatte bir çalışır
                    if (simdi - sonDerinTemizlik > TimeSpan.FromHours(6))
                    {
                        sonDerinTemizlik = simdi;
                        // 2. Süresi dolmuş sohbet susturmalarını temizle
                        await db.SohbetSusturmalari.Where(s => s.Bitis < simdi).ExecuteDeleteAsync(durdur);

                        // 3. 24 saatten eski 0 puanlık geçici okuma başlangıç (bolum_basla) kayıtlarını temizle
                        var birGunOnce = simdi.AddDays(-1);
                        await db.PuanKayitlari.Where(p => p.Puan == 0 && p.Tarih < birGunOnce).ExecuteDeleteAsync(durdur);

                        // 4. 30 günden eski bildirimleri temizle
                        var otuzGunOnce = simdi.AddDays(-30);
                        await db.Bildirimler.Where(b => b.Tarih < otuzGunOnce).ExecuteDeleteAsync(durdur);

                        // 5. Kapatılmış ve 7 günü geçmiş yorum şikayetlerini temizle
                        var yediGunOnce = simdi.AddDays(-7);
                        await db.YorumSikayetleri.Where(s => s.Durum == "kapali" && s.Tarih < yediGunOnce).ExecuteDeleteAsync(durdur);

                        // 6. Denetim kayıtlarında en güncel 500 satırı tut, daha eskileri sil
                        var besYuzuncuDenetimId = await db.DenetimKayitlari.AsNoTracking()
                            .OrderByDescending(d => d.Id)
                            .Skip(500 - 1)
                            .Select(d => (int?)d.Id)
                            .FirstOrDefaultAsync(durdur);
                        if (besYuzuncuDenetimId.HasValue)
                            await db.DenetimKayitlari.Where(d => d.Id < besYuzuncuDenetimId.Value).ExecuteDeleteAsync(durdur);
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { Console.WriteLine($"[Veritabanı temizlik] {ex.Message}"); }
                try { await Task.Delay(TimeSpan.FromMinutes(3), durdur); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
