using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models
{
    [Table("Students")]
    public class Student
    {
        [Key]
        [Display(Name = "Mã sinh viên (ID)")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "Mã sinh viên phải từ 2 đến 20 ký tự")]
        public string? StudentId { get; set; }

        [Display(Name = "Họ và tên")]
        [Required(ErrorMessage = "Vui lòng nhập Họ và tên")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự")]
        public string? FullName { get; set; }

        // --- Khóa ngoại liên kết bảng Faculty ---
        [Display(Name = "Khoa")]
        [Required(ErrorMessage = "Vui lòng chọn Khoa")]
        public string? FacultyId { get; set; }

        [ForeignKey("FacultyId")]
        [Display(Name = "Khoa")]
        public virtual Faculty? Faculty { get; set; }
        // ----------------------------------------

        [Display(Name = "Số điện thoại (SĐT)")]
        [Required(ErrorMessage = "Vui lòng nhập Số điện thoại")]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "SĐT chỉ được nhập chữ số, bắt đầu bằng 0 và gồm 10-11 số")]
        [StringLength(11, MinimumLength = 10, ErrorMessage = "Số điện thoại phải từ 10 đến 11 chữ số")]
        public string? PhoneNumber { get; set; }
    }
}
