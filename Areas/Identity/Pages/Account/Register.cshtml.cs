using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using DemoMVC.Models;

namespace DemoMVC.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        private static readonly System.Text.RegularExpressions.Regex StudentCodeRegex =
            new(@"^2\d{9}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
            [Display(Name = "Họ và tên")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập địa chỉ Email.")]
            [EmailAddress(ErrorMessage = "Địa chỉ Email không hợp lệ.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [StringLength(100, ErrorMessage = "{0} phải có độ dài từ {2} đến {1} ký tự.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; } = string.Empty;

            // Tư cách đăng ký: "SinhVien" (tự động duyệt) hoặc "GiangVien" (chờ Admin duyệt)
            [Display(Name = "Tư cách đăng ký")]
            public string AccountType { get; set; } = UserTypes.SinhVien;

            [Display(Name = "Mã sinh viên")]
            public string? StudentCode { get; set; }

            [Display(Name = "Mã cán bộ")]
            [StringLength(20, ErrorMessage = "Mã cán bộ tối đa 20 ký tự.")]
            public string? StaffCode { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu và mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            bool isLecturer = Input.AccountType == UserTypes.GiangVien;
            Input.AccountType = isLecturer ? UserTypes.GiangVien : UserTypes.SinhVien;
            Input.StudentCode = Input.StudentCode?.Trim();
            Input.StaffCode = Input.StaffCode?.Trim().ToUpperInvariant();

            if (isLecturer)
            {
                if (string.IsNullOrWhiteSpace(Input.StaffCode))
                {
                    ModelState.AddModelError("Input.StaffCode", "Vui lòng nhập Mã cán bộ.");
                }
                else if (_userManager.Users.Any(u => u.StaffCode == Input.StaffCode))
                {
                    ModelState.AddModelError("Input.StaffCode", $"Mã cán bộ '{Input.StaffCode}' đã được đăng ký.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Input.StudentCode) || !StudentCodeRegex.IsMatch(Input.StudentCode))
                {
                    ModelState.AddModelError("Input.StudentCode", "Mã sinh viên phải gồm đúng 10 chữ số và bắt đầu bằng số 2.");
                }
                else if (_userManager.Users.Any(u => u.StudentCode == Input.StudentCode))
                {
                    ModelState.AddModelError("Input.StudentCode", $"Mã sinh viên '{Input.StudentCode}' đã được đăng ký tài khoản.");
                }
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                user.FullName = Input.FullName.Trim();
                user.UserType = Input.AccountType;
                user.StudentCode = isLecturer ? null : Input.StudentCode;
                user.StaffCode = isLecturer ? Input.StaffCode : null;
                user.IsApproved = !isLecturer; // Sinh viên tự động duyệt, Giảng viên chờ Admin duyệt

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Người dùng {Email} đã tạo tài khoản mới thành công.", Input.Email);

                    // Sinh viên: gán ngay Role SinhVien. Giảng viên: chưa gán Role, chỉ gán "GiangVien" khi Admin phê duyệt
                    if (!isLecturer && await _roleManager.RoleExistsAsync("SinhVien"))
                    {
                        await _userManager.AddToRoleAsync(user, "SinhVien");
                    }

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl ?? string.Empty);
                    var emailBody = $@"
                        <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b;'>
                            <h2 style='color: #0d6efd;'>Xác nhận đăng ký tài khoản - DemoMVC HUMG</h2>
                            <p>Xin chào <strong>{HtmlEncoder.Default.Encode(user.FullName)}</strong>,</p>
                            <p>Cảm ơn bạn đã đăng ký tài khoản trên Hệ thống Quản lý Sinh viên HUMG. Vui lòng nhấn vào nút dưới đây để xác nhận địa chỉ Email của bạn:</p>
                            <p style='margin: 24px 0;'>
                                <a href='{encodedUrl}' style='background-color: #0d6efd; color: #ffffff; padding: 10px 20px; text-decoration: none; border-radius: 6px; font-weight: bold;'>
                                    Xác nhận tài khoản
                                </a>
                            </p>
                            <p style='font-size: 0.9em; color: #64748b;'>Hoặc sao chép đường dẫn sau vào trình duyệt:<br/>{encodedUrl}</p>
                        </div>";

                    await _emailSender.SendEmailAsync(Input.Email, "Xác nhận tài khoản - Hệ thống Quản lý Sinh viên HUMG", emailBody);

                    if (isLecturer)
                    {
                        TempData["StatusMessage"] = "Đăng ký tài khoản Giảng viên/Cán bộ thành công! Tài khoản của bạn đang chờ Quản trị viên (Admin) phê duyệt tư cách giảng viên trước khi có thể đăng nhập.";
                        return RedirectToPage("./Login", new { returnUrl });
                    }

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Không thể khởi tạo đối tượng '{nameof(ApplicationUser)}'.");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("Hệ thống yêu cầu UserStore hỗ trợ Email.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
