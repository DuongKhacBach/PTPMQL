using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DemoMVC.Models;

namespace DemoMVC.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public RegisterConfirmationModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string Email { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string? email, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound($"Không tìm thấy tài khoản với địa chỉ email '{email}'.");
            }

            Email = email;
            return Page();
        }
    }
}
