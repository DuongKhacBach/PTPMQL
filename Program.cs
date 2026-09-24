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

// Đảm bảo cấu trúc bảng trong CSDL SQLite luôn đồng bộ đầy đủ 4 trường
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Tự động bổ sung cột School vào bảng Persons nếu chưa có
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE Persons ADD COLUMN School TEXT;");
    }
    catch
    {
        // Cột đã tồn tại
    }

    try
    {
        // Làm sạch dữ liệu NULL cũ nếu có để tránh lỗi ánh xạ
        dbContext.Database.ExecuteSqlRaw("UPDATE Persons SET FullName = '' WHERE FullName IS NULL;");
        dbContext.Database.ExecuteSqlRaw("UPDATE Persons SET Address = '' WHERE Address IS NULL;");
        dbContext.Database.ExecuteSqlRaw("UPDATE Persons SET School = '' WHERE School IS NULL;");
    }
    catch
    {
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

// Route mặc định: Controller=Person -> Action=Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Person}/{action=Index}/{id?}");

app.Run();
