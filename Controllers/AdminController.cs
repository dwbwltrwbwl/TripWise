using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TripWise.Models;

namespace TripWise.Controllers
{
    [Route("Admin")]
    public class AdminController : Controller
    {
        private readonly TripWiseContext _context;

        public AdminController(TripWiseContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(string username, string password)
        {

            // Проверяем, что поля не пустые
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Введите email и пароль");
                return View();
            }

            // Поиск пользователя в базе по email
            var user = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .FirstOrDefaultAsync(u => u.Email == username);

            // Если пользователь не найден
            if (user == null)
            {
                Console.WriteLine($"Пользователь с email {username} не найден");
                ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                return View();
            }

            // Проверяем, что пользователь является админом
            if (user.IdRole != 1)
            {
                Console.WriteLine($"Пользователь {username} не является админом (роль: {user.IdRole})");
                ModelState.AddModelError("", "Доступ запрещен. Только для администраторов");
                return View();
            }

            // Хэшируем введенный пароль
            var inputHash = HashPassword(password);

            // Проверяем пароль
            if (user.PasswordHash != inputHash)
            {
                Console.WriteLine($"Неверный пароль для пользователя {username}");
                ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                return View();
            }

            // Успешная авторизация
            Console.WriteLine($"Успешный вход админа: {username}");

            // Создаем сессию
            HttpContext.Session.SetInt32("UserId", user.IdUser);
            HttpContext.Session.SetString("UserName", $"{user.LastName} {user.FirstName}");
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetInt32("UserRole", user.IdRole);

            // Устанавливаем куки для админа
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Append("AdminAuth", "true", cookieOptions);

            return RedirectToAction("Dashboard");
        }

        [HttpGet("Dashboard")]
        public IActionResult Dashboard()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpGet("Logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Вспомогательный метод для хэширования пароля
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
        
        [HttpGet("CheckUsers")]
        public async Task<IActionResult> CheckUsers()
        {
            var users = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .ToListAsync();

            var result = users.Select(u => new
            {
                Id = u.IdUser,
                Email = u.Email,
                Name = $"{u.LastName} {u.FirstName}",
                Role = u.IdRole,
                RoleName = u.IdRoleNavigation?.Name,
                PasswordHash = u.PasswordHash
            });

            return Json(result);
        }
    }
}