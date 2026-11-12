using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DemoMVC.Data;
using DemoMVC.Models;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext kết nối SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// Đăng ký dịch vụ MVC Controllers with Views & Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

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

    // SignIn settings
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Cấu hình Cookie xác thực
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// Cấu hình DataProtection lưu key tại ./keys
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"./keys"))
    .SetApplicationName("DemoMVC")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(14));

var app = builder.Build();

// Đảm bảo cấu trúc bảng trong CSDL SQLite luôn đồng bộ với Migrations và Seed Roles & Users mặc định
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    try
    {
        dbContext.Database.Migrate();
        await SeedRolesAndUsersAsync(services);

        var sampleExcelPath = Path.Combine(Directory.GetCurrentDirectory(), "DanhSach_100_SinhVien_HUMG.xlsx");
        if (!File.Exists(sampleExcelPath))
        {
            using var sampleWorkbook = await DemoMVC.Controllers.StudentController.BuildSample100StudentsWorkbookAsync(dbContext);
            sampleWorkbook.SaveAs(sampleExcelPath);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB Init Note: {ex.Message}");
    }
}

// Cấu hình HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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

// 3. Route mặc định: / hoặc /{controller}/{action}/{id?}
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Student}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "Admin", "GiangVien", "SinhVien" };
    foreach (var roleName in roles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
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
            if (!await userManager.IsInRoleAsync(user, item.Role))
            {
                await userManager.AddToRoleAsync(user, item.Role);
            }
        }
    }
}
