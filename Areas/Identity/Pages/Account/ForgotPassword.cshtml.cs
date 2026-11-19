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
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

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
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Không tiết lộ thông tin tài khoản tồn tại hay chưa xác nhận
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl ?? string.Empty);
                var emailBody = $@"
                    <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b;'>
                        <h2 style='color: #0d6efd;'>Đặt lại mật khẩu - DemoMVC HUMG</h2>
                        <p>Xin chào <strong>{HtmlEncoder.Default.Encode(user.FullName)}</strong>,</p>
                        <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng nhấp vào nút bên dưới để thiết lập mật khẩu mới:</p>
                        <p style='margin: 24px 0;'>
                            <a href='{encodedUrl}' style='background-color: #0d6efd; color: #ffffff; padding: 10px 20px; text-decoration: none; border-radius: 6px; font-weight: bold;'>
                                Đặt lại mật khẩu
                            </a>
                        </p>
                        <p style='font-size: 0.9em; color: #64748b;'>Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.<br/>Liên kết: {encodedUrl}</p>
                    </div>";

                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "Đặt lại mật khẩu - Hệ thống Quản lý Sinh viên HUMG",
                    emailBody);

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
