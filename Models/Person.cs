using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models
{
    [Table("Persons")]
    public class Person
    {
        [Key]
        [Display(Name = "Mã Person (ID)")]
        [Required(ErrorMessage = "Vui lòng nhập Mã Person")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã định danh phải từ 2 đến 50 ký tự")]
        public string? PersonId { get; set; }

        [Display(Name = "Họ và tên")]
        [Required(ErrorMessage = "Vui lòng nhập Họ và tên")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự")]
        public string? FullName { get; set; }

        [Display(Name = "Địa chỉ")]
        [StringLength(200, ErrorMessage = "Địa chỉ không được vượt quá 200 ký tự")]
        public string? Address { get; set; }
    }
}
