using Microsoft.AspNetCore.Mvc;

namespace TripWise.Controllers
{
    [Route("Admin")]
    public class AdminController : Controller
    {
        [HttpGet("")]
        [HttpGet("Login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost("Login")]
        public IActionResult Login(string username, string password)
        {
            return RedirectToAction("Dashboard");
        }

        [HttpGet("Dashboard")]
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}