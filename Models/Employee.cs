using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models
{
    [Table("Employees")]
    public class Employee
    {
        [Key]
        [Display(Name = "Mã nhân viên (ID)")]
        [Required(ErrorMessage = "Vui lòng nhập Mã nhân viên")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "Mã nhân viên phải từ 2 đến 20 ký tự")]
        public string? EmployeeId { get; set; }

        [Display(Name = "Họ và tên")]
        [Required(ErrorMessage = "Vui lòng nhập Họ và tên")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự")]
        public string? FullName { get; set; }

        [Display(Name = "Tuổi")]
        [Required(ErrorMessage = "Vui lòng nhập Tuổi")]
        [Range(18, 65, ErrorMessage = "Tuổi nhân viên phải nằm trong khoảng từ 18 đến 65")]
        public int Age { get; set; }

        [Display(Name = "Địa chỉ")]
        [StringLength(200, ErrorMessage = "Địa chỉ không được vượt quá 200 ký tự")]
        public string? Address { get; set; }
    }
}
