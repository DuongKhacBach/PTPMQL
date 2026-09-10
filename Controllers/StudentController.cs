using Microsoft.AspNetCore.Mvc;
using DemoMVC.Models;

namespace DemoMVC.Controllers;

[Route("sinh-vien")]
public class StudentController : Controller
{
    // GET: /sinh-vien/trang-chu
    [HttpGet]
    [Route("trang-chu")]
    [Route("")]
    public IActionResult Index()
    {
        return View();
    }

    // POST: /sinh-vien/trang-chu
    [HttpPost]
    [Route("trang-chu")]
    [Route("")]
    public IActionResult Index(string fullName, string address, string school, string gender)
    {
        string result = $"Họ tên: {fullName} - Địa chỉ: {address} - Trường: {school} - Giới tính: {gender}";
        
        ViewBag.Message = result;
        ViewBag.thongBao = result;
        ViewBag.FullName = fullName;
        ViewBag.Address = address;
        ViewBag.School = school;
        ViewBag.Gender = gender;

        return View();
    }
}
