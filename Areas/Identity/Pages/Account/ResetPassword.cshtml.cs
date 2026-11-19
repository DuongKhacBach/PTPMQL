using System.ComponentModel.DataAnnotations;
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
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập địa chỉ Email.")]
            [EmailAddress(ErrorMessage = "Địa chỉ Email không hợp lệ.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
            [StringLength(100, ErrorMessage = "{0} phải có độ dài từ {2} đến {1} ký tự.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu mới")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu mới")]
            [Compare("Password", ErrorMessage = "Mật khẩu mới và mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required]
            public string Code { get; set; } = string.Empty;
        }

        public IActionResult OnGet(string? code = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Cần có mã xác thực hợp lệ để đặt lại mật khẩu.");
            }

            try
            {
                Input = new InputModel
                {
                    Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
                };
                return Page();
            }
            catch
            {
                return BadRequest("Mã đặt lại mật khẩu không hợp lệ.");
            }
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
                // Không tiết lộ sự tồn tại của tài khoản
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
