using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers
{
    public class DemoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
