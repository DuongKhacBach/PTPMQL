using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DemoMVC.Data;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext kết nối SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// Đăng ký dịch vụ MVC Controllers with Views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Đảm bảo cấu trúc bảng trong CSDL SQLite luôn đồng bộ với Migrations
// (tự động tạo bảng Students: StudentId, FullName, Faculty, PhoneNumber)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        dbContext.Database.Migrate();
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

app.Run();
