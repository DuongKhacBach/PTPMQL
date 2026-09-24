using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models
{
    [Table("Persons")]
    public class Person
    {
        [Key]
        [Display(Name = "Mã định danh (ID)")]
        [Required(ErrorMessage = "Vui lòng nhập Mã định danh (ID)")]
        public string? PersonId { get; set; }

        [Display(Name = "Họ và tên")]
        [Required(ErrorMessage = "Vui lòng nhập Họ và tên")]
        public string? FullName { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Trường học")]
        public string? School { get; set; }
    }
}
