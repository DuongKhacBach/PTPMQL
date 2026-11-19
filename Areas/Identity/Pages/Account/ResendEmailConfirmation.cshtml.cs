using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using DemoMVC.Models;

namespace DemoMVC.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ResendEmailConfirmationModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập địa chỉ Email.")]
            [EmailAddress(ErrorMessage = "Địa chỉ Email không hợp lệ.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                StatusMessage = "Nếu địa chỉ email tồn tại trong hệ thống, liên kết xác nhận đã được gửi. Vui lòng kiểm tra hộp thư của bạn.";
                return Page();
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = userId, code = code },
                protocol: Request.Scheme);

            var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl ?? string.Empty);
            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b;'>
                    <h2 style='color: #0d6efd;'>Gửi lại liên kết xác nhận tài khoản - DemoMVC HUMG</h2>
                    <p>Xin chào <strong>{HtmlEncoder.Default.Encode(user.FullName)}</strong>,</p>
                    <p>Vui lòng nhấn vào nút bên dưới để xác nhận địa chỉ email cho tài khoản của bạn:</p>
                    <p style='margin: 24px 0;'>
                        <a href='{encodedUrl}' style='background-color: #0d6efd; color: #ffffff; padding: 10px 20px; text-decoration: none; border-radius: 6px; font-weight: bold;'>
                            Xác nhận tài khoản
                        </a>
                    </p>
                    <p style='font-size: 0.9em; color: #64748b;'>Hoặc sao chép liên kết sau vào trình duyệt:<br/>{encodedUrl}</p>
                </div>";

            await _emailSender.SendEmailAsync(
                Input.Email,
                "Xác nhận tài khoản - Hệ thống Quản lý Sinh viên HUMG",
                emailBody);

            StatusMessage = "Email xác nhận đã được gửi lại thành công. Vui lòng kiểm tra hộp thư của bạn.";
            return Page();
        }
    }
}
