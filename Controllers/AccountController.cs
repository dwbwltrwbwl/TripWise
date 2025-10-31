using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;

namespace TripWise.Controllers
{
    public class AccountController : Controller
    {
        private readonly TripWiseContext _context;

        public AccountController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public IActionResult Login(string email, string password, bool rememberMe)
        {
            // Авторизация временно отключена
            ModelState.AddModelError("", "Авторизация временно недоступна");
            return View();
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public IActionResult Register(string fullName, string email, string password, string confirmPassword, bool agreeTerms)
        {
            // Регистрация временно отключена
            ModelState.AddModelError("", "Регистрация временно недоступна");
            return View();
        }
    }
}