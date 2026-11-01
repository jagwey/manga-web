using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kage95.Backend.Services
{
    public interface IDiscordServisi
    {
        Task YeniMangaBildirAsync(int mangaId, string mangaAdi, string? kapakResmi, string? aciklama);
        Task YeniBolumBildirAsync(int mangaId, string mangaAdi, decimal bolumNo, string? bolumAdi, string? kapakResmi, int bolumId);
        Task DuyuruBildirAsync(string baslik, string? icerik, string? etiket, bool onemli);
        Task KullaniciRolSyncAsync(string discordId, int okunanBolumSayisi, int puan, string? unvan = null);
    }

    public class DiscordServisi : IDiscordServisi
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<DiscordServisi> _logger;

        public DiscordServisi(HttpClient http, IConfiguration config, ILogger<DiscordServisi> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        private string BotUrl => _config["Discord:BotUrl"]?.TrimEnd('/') ?? "https://kage-discord-bot.onrender.com";
        private string WebhookSecret => _config["Discord:WebhookSecret"] ?? "kage95_secret_key";
        private string? DirectWebhookUrl => _config["Discord:DirectWebhookUrl"];
        private string SiteUrl => (_config["SiteUrl"] ?? "https://kage95.com").TrimEnd('/');

        private string TamGorselUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return $"{SiteUrl}/favicon.svg";
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            return $"{SiteUrl}/{url.TrimStart('/')}";
        }

        public async Task YeniMangaBildirAsync(int mangaId, string mangaAdi, string? kapakResmi, string? aciklama)
        {
            var mangaUrl = $"{SiteUrl}/manga.html?id={mangaId}";
            var gorsel = TamGorselUrl(kapakResmi);

            // 1. Doğrudan Discord Webhook (En hızlı ve %100 garantili yol)
            if (!string.IsNullOrWhiteSpace(DirectWebhookUrl))
            {
                try
                {
                    var discordPayload = new
                    {
                        username = "Kage 95",
                        avatar_url = $"{SiteUrl}/favicon.svg",
                        content = $"⛩️ **YENİ MANGA KÜTÜPHANEYE EKLENDİ!**",
                        embeds = new[]
                        {
                            new
                            {
                                title = $"🔥 {mangaAdi}",
                                url = mangaUrl,
                                description = string.IsNullOrWhiteSpace(aciklama) ? "Kage 95 kütüphanesine yepyeni bir efsane katıldı! Hemen ilk okuyanlardan ol." : aciklama,
                                color = 15022389, // #e53935 Kan Kırmızısı
                                thumbnail = new { url = gorsel },
                                fields = new[]
                                {
                                    new { name = "🗡️ Seri Adı", value = $"**{mangaAdi}**", inline = true },
                                    new { name = "🌐 Platform", value = $"[kage95.com]({SiteUrl})", inline = true }
                                },
                                footer = new { text = "Kage 95 Manga Platformu • kage95.com" }
                            }
                        }
                    };

                    var resp = await _http.PostAsJsonAsync(DirectWebhookUrl, discordPayload);
                    if (resp.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("[DiscordServisi] Yeni manga doğrudan Discord Webhook ile iletildi: {Manga}", mangaAdi);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DiscordServisi] Doğrudan Discord Webhook iletiminde hata.");
                }
            }

            // 2. Yedek: Bot Webhook API
            try
            {
                if (!string.IsNullOrWhiteSpace(BotUrl))
                {
                    var payload = new
                    {
                        mangaId,
                        mangaAdi,
                        kapakResmi = gorsel,
                        aciklama,
                        mangaUrl
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{BotUrl}/api/webhook/manga");
                    request.Headers.Add("x-kage-secret", WebhookSecret);
                    request.Content = JsonContent.Create(payload);

                    var response = await _http.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("[DiscordServisi] Yeni manga Kage Bot Webhook'a iletildi: {Manga}", mangaAdi);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DiscordServisi] Bot Webhook iletiminde hata.");
            }
        }

        public async Task YeniBolumBildirAsync(int mangaId, string mangaAdi, decimal bolumNo, string? bolumAdi, string? kapakResmi, int bolumId)
        {
            var okumaUrl = $"{SiteUrl}/read.html?mangaId={mangaId}&chapterId={bolumId}";
            var gorsel = TamGorselUrl(kapakResmi);
            var baslik = string.IsNullOrWhiteSpace(bolumAdi) ? $"Bölüm {bolumNo}" : bolumAdi;

            // 1. Doğrudan Discord Webhook
            if (!string.IsNullOrWhiteSpace(DirectWebhookUrl))
            {
                try
                {
                    var discordPayload = new
                    {
                        username = "Kage 95",
                        avatar_url = $"{SiteUrl}/favicon.svg",
                        content = $"⚔️ **YENİ BÖLÜM YAYINDA!** `{mangaAdi}`",
                        embeds = new[]
                        {
                            new
                            {
                                title = $"📖 {mangaAdi} - {baslik}",
                                url = okumaUrl,
                                description = "Gölgeler arasından yeni sayfalar çıktı! Hemen okumaya başla.",
                                color = 15022389,
                                thumbnail = new { url = gorsel },
                                fields = new[]
                                {
                                    new { name = "🗡️ Seri", value = mangaAdi, inline = true },
                                    new { name = "📜 Bölüm", value = $"Bölüm {bolumNo}", inline = true }
                                },
                                footer = new { text = "Kage 95 Manga Platformu • kage95.com" }
                            }
                        }
                    };

                    var resp = await _http.PostAsJsonAsync(DirectWebhookUrl, discordPayload);
                    if (resp.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("[DiscordServisi] Yeni bölüm doğrudan Discord Webhook ile iletildi: {Manga} B.{No}", mangaAdi, bolumNo);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DiscordServisi] Doğrudan Discord Webhook bölüm iletiminde hata.");
                }
            }

            // 2. Yedek: Bot Webhook API
            try
            {
                if (!string.IsNullOrWhiteSpace(BotUrl))
                {
                    var payload = new
                    {
                        mangaId,
                        mangaAdi,
                        bolumNo,
                        bolumAdi = baslik,
                        kapakResmi = gorsel,
                        bolumId,
                        okumaUrl
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{BotUrl}/api/webhook/bolum");
                    request.Headers.Add("x-kage-secret", WebhookSecret);
                    request.Content = JsonContent.Create(payload);

                    await _http.SendAsync(request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DiscordServisi] Bot Webhook bölüm iletiminde hata.");
            }
        }

        public async Task DuyuruBildirAsync(string baslik, string? icerik, string? etiket, bool onemli)
        {
            if (!string.IsNullOrWhiteSpace(DirectWebhookUrl))
            {
                try
                {
                    var discordPayload = new
                    {
                        username = "Kage 95",
                        avatar_url = $"{SiteUrl}/favicon.svg",
                        content = onemli ? "🚨 **ÖNEMLİ KLAN FERMANI**" : "📢 **KAGE SİTE DUYURUSU**",
                        embeds = new[]
                        {
                            new
                            {
                                title = baslik,
                                description = icerik ?? "",
                                color = onemli ? 16763904 : 15022389,
                                fields = new[]
                                {
                                    new { name = "📜 Kategori", value = etiket ?? "Genel", inline = true }
                                },
                                footer = new { text = "Kage 95 Platformu • kage95.com" }
                            }
                        }
                    };

                    await _http.PostAsJsonAsync(DirectWebhookUrl, discordPayload);
                    return;
                }
                catch {}
            }

            try
            {
                if (string.IsNullOrWhiteSpace(BotUrl)) return;
                var payload = new { baslik, icerik, etiket = etiket ?? "Genel", onemli };
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{BotUrl}/api/webhook/duyuru");
                request.Headers.Add("x-kage-secret", WebhookSecret);
                request.Content = JsonContent.Create(payload);
                await _http.SendAsync(request);
            }
            catch {}
        }

        public async Task KullaniciRolSyncAsync(string discordId, int okunanBolumSayisi, int puan, string? unvan = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BotUrl) || string.IsNullOrWhiteSpace(discordId)) return;
                var payload = new { discordId, okunanBolumSayisi, puan, unvan };
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{BotUrl}/api/webhook/rol-sync");
                request.Headers.Add("x-kage-secret", WebhookSecret);
                request.Content = JsonContent.Create(payload);
                await _http.SendAsync(request);
            }
            catch {}
        }
    }
}
