using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DemoMVC.Models.Process
{
    public class SendMailService : IEmailSender
    {
        private readonly MailSettings _mailSettings;
        private readonly ILogger<SendMailService> _logger;
        private readonly IWebHostEnvironment _env;

        public SendMailService(
            IOptions<MailSettings> mailSettings,
            ILogger<SendMailService> logger,
            IWebHostEnvironment env)
        {
            _mailSettings = mailSettings.Value;
            _logger = logger;
            _env = env;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var fromAddress = IsValidEmailFormat(_mailSettings.Mail)
                ? _mailSettings.Mail.Trim()
                : "noreply@demomvc.local";
            var displayName = string.IsNullOrWhiteSpace(_mailSettings.DisplayName)
                ? "DemoMVC HUMG"
                : _mailSettings.DisplayName.Trim();

            var message = new MimeMessage();
            message.Sender = new MailboxAddress(displayName, fromAddress);
            message.From.Add(new MailboxAddress(displayName, fromAddress));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };
            message.Body = builder.ToMessageBody();

            if (IsPlaceholderOrMissingCredentials())
            {
                _logger.LogWarning(
                    "Chưa cấu hình MailSettings (Gmail / App Password) hợp lệ. Bỏ qua kết nối SMTP và lưu email vào thư mục mailsSave/.");
                await SaveEmailToDiskAsync(message, email);
                return;
            }

            using var smtp = new SmtpClient();
            smtp.Timeout = 10000;

            try
            {
                await smtp.ConnectAsync(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_mailSettings.Mail.Trim(), _mailSettings.Password.Trim());
                await smtp.SendAsync(new SendRequest(message));
                await smtp.DisconnectAsync(true);

                _logger.LogInformation("Đã gửi email thành công tới {Email} với tiêu đề '{Subject}'.", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Gửi email qua SMTP ({Host}:{Port}) tới {Email} thất bại. Hệ thống sẽ lưu bản sao .eml vào thư mục mailsSave/.",
                    _mailSettings.Host, _mailSettings.Port, email);
                await SaveEmailToDiskAsync(message, email);
            }
        }

        private bool IsPlaceholderOrMissingCredentials()
        {
            if (string.IsNullOrWhiteSpace(_mailSettings.Mail) ||
                string.IsNullOrWhiteSpace(_mailSettings.Password) ||
                string.IsNullOrWhiteSpace(_mailSettings.Host))
            {
                return true;
            }

            var mailLower = _mailSettings.Mail.Trim().ToLowerInvariant();
            var passLower = _mailSettings.Password.Trim().ToLowerInvariant();

            return mailLower.Contains("your-email")
                || mailLower.Contains("example.com")
                || passLower.Contains("your-app-password")
                || passLower.Contains("your_password")
                || passLower.Contains("app-password");
        }

        private static bool IsValidEmailFormat(string? email)
        {
            return !string.IsNullOrWhiteSpace(email) && MailboxAddress.TryParse(email.Trim(), out _);
        }

        private async Task SaveEmailToDiskAsync(MimeMessage message, string recipientEmail)
        {
            try
            {
                var saveDir = Path.Combine(_env.ContentRootPath, "mailsSave");
                Directory.CreateDirectory(saveDir);

                var safeRecipient = new string(recipientEmail
                    .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                    .ToArray());
                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeRecipient}_{Guid.NewGuid():N}.eml";
                var emailSaveFile = Path.Combine(saveDir, fileName);

                await message.WriteToAsync(emailSaveFile);
                _logger.LogInformation("Đã lưu email dự phòng tại: {FilePath}", emailSaveFile);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Không thể lưu file .eml dự phòng vào thư mục mailsSave/.");
            }
        }
    }
}
