using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models
{
    [Table("Faculties")]
    public class Faculty
    {
        [Key]
        [Display(Name = "Mã Khoa")]
        [StringLength(10)]
        public string FacultyId { get; set; } = default!;

        [Display(Name = "Mã số ngành (2 chữ số)")]
        [Required(ErrorMessage = "Vui lòng nhập mã số ngành")]
        [RegularExpression(@"^\d{2}$", ErrorMessage = "Mã ngành phải gồm đúng 2 chữ số (VD: 01, 05)")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Mã ngành phải đúng 2 ký tự")]
        public string FacultyCode { get; set; } = default!; // Dùng để sinh mã SV tự động

        [Display(Name = "Tên Khoa")]
        [Required(ErrorMessage = "Vui lòng nhập Tên Khoa")]
        [StringLength(150, ErrorMessage = "Tên khoa không được vượt quá 150 ký tự")]
        public string FacultyName { get; set; } = default!;

        // Quan hệ 1 - Nhiều: Một khoa có nhiều sinh viên
        public virtual ICollection<Student>? Students { get; set; }
    }
}
