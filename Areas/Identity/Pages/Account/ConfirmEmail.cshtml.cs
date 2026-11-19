using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using DemoMVC.Models;

namespace DemoMVC.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [TempData]
        public string? StatusMessage { get; set; }

        public bool IsSucceeded { get; set; }

        public async Task<IActionResult> OnGetAsync(string? userId, string? code)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                IsSucceeded = false;
                StatusMessage = "Liên kết xác nhận email không hợp lệ hoặc đã thiếu tham số.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin tài khoản với ID '{userId}'.");
            }

            try
            {
                code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                var result = await _userManager.ConfirmEmailAsync(user, code);
                IsSucceeded = result.Succeeded;
                StatusMessage = result.Succeeded
                    ? "Cảm ơn bạn! Địa chỉ email của bạn đã được xác nhận thành công. Bạn có thể đăng nhập ngay bây giờ."
                    : "Xác nhận email thất bại. Liên kết có thể đã hết hạn hoặc không hợp lệ.";
            }
            catch
            {
                IsSucceeded = false;
                StatusMessage = "Mã xác nhận email không đúng định dạng.";
            }

            return Page();
        }
    }
}
