using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Models;

namespace DemoMVC.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Faculty> Faculty { get; set; } = default!;
        public DbSet<Student> Student { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình quan hệ 1-Nhiều giữa Faculty và Student
            modelBuilder.Entity<Student>()
                .HasOne(s => s.Faculty)
                .WithMany(f => f.Students)
                .HasForeignKey(s => s.FacultyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Dữ liệu ban đầu (Seed Data) các Khoa của Trường Đại học Mỏ - Địa chất (HUMG)
            modelBuilder.Entity<Faculty>().HasData(
                new Faculty { FacultyId = "CNTT", FacultyCode = "05", FacultyName = "Công nghệ thông tin" },
                new Faculty { FacultyId = "MO",   FacultyCode = "01", FacultyName = "Mỏ" },
                new Faculty { FacultyId = "DC",   FacultyCode = "02", FacultyName = "Địa chất" },
                new Faculty { FacultyId = "DK",   FacultyCode = "03", FacultyName = "Dầu khí" },
                new Faculty { FacultyId = "KT",   FacultyCode = "04", FacultyName = "Kinh tế & Quản trị kinh doanh" },
                new Faculty { FacultyId = "CD",   FacultyCode = "06", FacultyName = "Cơ - Điện" },
                new Faculty { FacultyId = "XD",   FacultyCode = "07", FacultyName = "Xây dựng" },
                new Faculty { FacultyId = "MT",   FacultyCode = "08", FacultyName = "Môi trường" },
                new Faculty { FacultyId = "TDBD", FacultyCode = "09", FacultyName = "Trắc địa - Bản đồ và Quản lý đất đai" }
            );
        }
    }
}
