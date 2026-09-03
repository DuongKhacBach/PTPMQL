using Microsoft.AspNetCore.Mvc;
using DemoMVC.Models;

namespace DemoMVC.Controllers;

public class StudentController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
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
