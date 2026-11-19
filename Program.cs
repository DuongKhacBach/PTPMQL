using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.Process;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext kết nối SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));
});

// Đăng ký dịch vụ MVC Controllers with Views & Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Xuất ký tự tiếng Việt UTF-8 trực tiếp trong HTML (vẫn giữ nguyên mã hóa chống XSS cho <, >, &, ", ')
builder.Services.Configure<WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(
        UnicodeRanges.BasicLatin,
        UnicodeRanges.Latin1Supplement,
        UnicodeRanges.LatinExtendedA,
        UnicodeRanges.LatinExtendedB,
        UnicodeRanges.LatinExtendedAdditional);
});

// Cấu hình gửi Email qua MailKit (STT 13)
builder.Services.AddOptions();
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
builder.Services.AddTransient<IEmailSender, SendMailService>();

// Cấu hình ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = false;

    // SignIn settings (Bắt buộc xác thực Email khi đăng ký mới)
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedPhoneNumber = false;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Tự động kiểm tra lại SecurityStamp định kỳ để cập nhật Role/Claim nhanh chóng
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromSeconds(10);
});

// Cấu hình Phân quyền động (Policy-based Authorization) theo từng Action (STT 14)
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in SystemPermissions.GetAll())
    {
        options.AddPolicy(permission.Value, policy =>
            policy.RequireClaim(SystemPermissions.ClaimType, permission.Value));
    }
});

// Cấu hình Cookie xác thực an toàn (tương thích cả HTTP trên MonsterASP FreeSite và HTTPS)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// Cấu hình DataProtection lưu key theo ContentRootPath (ổn định trên cả Local và IIS/MonsterASP)
var keysDir = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("DemoMVC")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(14));

var app = builder.Build();

// Đảm bảo cấu trúc bảng trong CSDL SQLite luôn đồng bộ với Migrations và Seed Roles, RoleClaims & Users mặc định
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        dbContext.Database.Migrate();
        await SeedRolesAndUsersAsync(services, logger);
        await DemoMVC.Controllers.StudentController.FixInvalidPhoneNumbersAsync(dbContext);

        var sampleExcelPath = Path.Combine(app.Environment.ContentRootPath, "DanhSach_100_SinhVien_HUMG.xlsx");
        if (!File.Exists(sampleExcelPath))
        {
            using var sampleWorkbook = await DemoMVC.Controllers.StudentController.BuildSample100StudentsWorkbookAsync(dbContext);
            sampleWorkbook.SaveAs(sampleExcelPath);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi khởi tạo CSDL SQLite hoặc seed dữ liệu mặc định.");
    }
}

// Cấu hình HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseHttpsRedirection();
}

// Security Response Headers cơ bản
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// Trang báo lỗi thân thiện (404/403/400) cho truy cập URL thông thường (tắt với request AJAX để giữ nguyên HTTP status code)
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
app.Use(async (context, next) =>
{
    if (context.Request.IsAjaxRequest())
    {
        var statusCodeFeature = context.Features.Get<IStatusCodePagesFeature>();
        if (statusCodeFeature != null)
        {
            statusCodeFeature.Enabled = false;
        }
    }
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 1. Route cho: /sinhvien, /sinhvien/Index, /sinhvien/Create, /sinhvien/Edit/{id}...
app.MapControllerRoute(
    name: "sinhvien",
    pattern: "sinhvien/{action=Index}/{id?}",
    defaults: new { controller = "Student" });

// 2. Route cho: /Index (truy cập thẳng Index của Student)
app.MapControllerRoute(
    name: "directIndex",
    pattern: "Index",
    defaults: new { controller = "Student", action = "Index" });

// 3. Route mặc định: / về Trang chủ (Home/Index) hoặc /{controller}/{action}/{id?}
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider, ILogger logger)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "Admin", "GiangVien", "SinhVien" };
    foreach (var roleName in roles)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            role = new IdentityRole(roleName);
            var roleCreateResult = await roleManager.CreateAsync(role);
            if (!roleCreateResult.Succeeded)
            {
                logger.LogWarning("Không thể tạo Role mặc định '{RoleName}': {Errors}",
                    roleName, string.Join("; ", roleCreateResult.Errors.Select(e => e.Description)));
                continue;
            }
        }

        // Seed RoleClaims mặc định vào AspNetRoleClaims nếu Role chưa có Permission claim nào
        var existingClaims = await roleManager.GetClaimsAsync(role);
        if (!existingClaims.Any(c => c.Type == SystemPermissions.ClaimType))
        {
            var defaultPermissions = SystemPermissions.GetDefaultPermissionsForRole(roleName);
            foreach (var permission in defaultPermissions)
            {
                await roleManager.AddClaimAsync(role, new Claim(SystemPermissions.ClaimType, permission));
            }
            logger.LogInformation("Đã seed {Count} Permission Claims mặc định cho Role '{RoleName}'.",
                defaultPermissions.Count, roleName);
        }
    }

    var seedUsers = new (string Email, string Password, string FullName, string Role)[]
    {
        ("admin@humg.edu.vn", "Admin1234", "Quản trị viên", "Admin"),
        ("giangvien@humg.edu.vn", "Giangvien123", "Giảng viên HUMG", "GiangVien"),
        ("sinhvien@humg.edu.vn", "Sinhvien123", "Sinh viên HUMG", "SinhVien")
    };

    foreach (var item in seedUsers)
    {
        var user = await userManager.FindByEmailAsync(item.Email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = item.Email,
                Email = item.Email,
                FullName = item.FullName,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user, item.Password);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(user, item.Role);
            }
        }
        else
        {
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }
            if (!await userManager.IsInRoleAsync(user, item.Role))
            {
                await userManager.AddToRoleAsync(user, item.Role);
            }
        }
    }
}
