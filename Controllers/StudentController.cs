using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers;

public class StudentController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
