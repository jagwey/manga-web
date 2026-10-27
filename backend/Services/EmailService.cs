using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Kage95.Backend.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string alici, string konu, string htmlIcerik, CancellationToken ct = default);
    }

    /// <summary>Brevo SMTP üzerinden (EmailSettings bölümü) HTML e-posta gönderir.</summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string alici, string konu, string htmlIcerik, CancellationToken ct = default)
        {
            var s = _config.GetSection("EmailSettings");
            var sunucu = s["Server"];
            var kullanici = s["Username"];
            var parola = s["Password"];
            if (string.IsNullOrWhiteSpace(sunucu) || string.IsNullOrWhiteSpace(kullanici) || string.IsNullOrWhiteSpace(parola))
                throw new InvalidOperationException("EmailSettings ayarlanmamış (Server, Username, Password).");

            var mesaj = new MimeMessage();
            mesaj.From.Add(new MailboxAddress(s["SenderName"] ?? "Kage", s["SenderEmail"] ?? kullanici));
            mesaj.To.Add(MailboxAddress.Parse(alici));
            mesaj.Subject = konu;
            mesaj.Body = new BodyBuilder { HtmlBody = htmlIcerik }.ToMessageBody();

            using var istemci = new SmtpClient();
            await istemci.ConnectAsync(sunucu, s.GetValue("Port", 587), SecureSocketOptions.StartTls, ct);
            await istemci.AuthenticateAsync(kullanici, parola, ct);
            await istemci.SendAsync(mesaj, ct);
            await istemci.DisconnectAsync(true, ct);
        }
    }
}
