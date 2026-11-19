using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace DemoMVC.Models
{
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        // New properties for user type management
        [PersonalData]
        [Display(Name = "Loại người dùng")]
        public string UserType { get; set; } = "SinhVien"; // "Admin", "GiangVien", "SinhVien"

        [PersonalData]
        [Display(Name = "Mã sinh viên")]
        public string? StudentCode { get; set; }

        [PersonalData]
        [Display(Name = "Mã cán bộ")]
        public string? StaffCode { get; set; }

        [PersonalData]
        [Display(Name = "Đã duyệt")]
        public bool IsApproved { get; set; } = true;
    }
}
