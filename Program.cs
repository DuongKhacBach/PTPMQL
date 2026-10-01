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

// Đảm bảo cấu trúc bảng trong CSDL SQLite luôn đồng bộ và hợp lệ
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Tự động tạo bảng Employees với 4 trường: EmployeeId, FullName, Age, Address
        dbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Employees"" (
                ""EmployeeId"" TEXT NOT NULL PRIMARY KEY,
                ""FullName"" TEXT NOT NULL,
                ""Age"" INTEGER NOT NULL,
                ""Address"" TEXT NULL
            );
        ");
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

// Route mặc định: Controller=Employee -> Action=Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Employee}/{action=Index}/{id?}");

app.Run();
