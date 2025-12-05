using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
        // GET: /Account/Edit
        [HttpGet]
        public IActionResult Edit()
        {
            // Просто показываем страницу без модели
            return View();
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            // Просто показываем страницу без модели
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string rememberMe)
        {
            // Валидация
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email и пароль обязательны для заполнения");
                return View();
            }

            // Проверка email на валидность
            if (!IsValidEmail(email))
            {
                ModelState.AddModelError("", "Введите корректный email адрес");
                return View();
            }

            try
            {
                // Хэшируем введенный пароль для сравнения
                var hashedPassword = HashPassword(password);

                // Ищем пользователя в базе
                var user = await _context.Users
                    .Include(u => u.IdRoleNavigation)
                    .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hashedPassword);

                if (user != null)
                {
                    // Успешная авторизация
                    // Сохраняем информацию о пользователе в сессии
                    HttpContext.Session.SetInt32("UserId", user.IdUser);
                    HttpContext.Session.SetString("UserName", user.Name);
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetInt32("UserRole", user.IdRole);

                    // Если нужно запомнить пользователя, можно установить куки
                    bool remember = !string.IsNullOrEmpty(rememberMe) && rememberMe == "on";
                    if (remember)
                    {
                        // Установка долгосрочных куки (например, на 30 дней)
                        var cookieOptions = new CookieOptions
                        {
                            Expires = DateTime.Now.AddDays(30),
                            HttpOnly = true,
                            IsEssential = true
                        };
                        Response.Cookies.Append("UserEmail", user.Email, cookieOptions);
                    }

                    // Редирект на главную страницу
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Неверный email или пароль");
                    return View();
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Console.WriteLine($"Ошибка при авторизации: {ex.Message}");
                ModelState.AddModelError("", "Произошла ошибка при авторизации. Попробуйте еще раз.");
                return View();
            }
        }

        // GET: /Account/Logout
        public IActionResult Logout()
        {
            // Очищаем сессию
            HttpContext.Session.Clear();

            // Удаляем куки
            Response.Cookies.Delete("UserEmail");

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(string fullName, string email, string password, string confirmPassword, string agreeTerms)
        {
            // Валидация
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ModelState.AddModelError("", "Все поля обязательны для заполнения");
                return View();
            }

            // Проверка email на валидность
            if (!IsValidEmail(email))
            {
                ModelState.AddModelError("", "Введите корректный email адрес");
                return View();
            }

            // Проверка пароля
            var passwordValidationResult = ValidatePassword(password);
            if (!passwordValidationResult.IsValid)
            {
                ModelState.AddModelError("", passwordValidationResult.ErrorMessage);
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Пароли не совпадают");
                return View();
            }

            // Проверяем, что чекбокс отмечен (значение "on")
            if (string.IsNullOrEmpty(agreeTerms) || agreeTerms != "on")
            {
                ModelState.AddModelError("", "Необходимо согласие с условиями использования");
                return View();
            }

            // Проверка существования пользователя
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Пользователь с таким email уже существует");
                return View();
            }

            try
            {
                // Создание нового пользователя
                var user = new User
                {
                    Name = fullName,
                    Email = email,
                    PasswordHash = HashPassword(password),
                    Age = null,
                    CreatedAt = DateTime.UtcNow,
                    IdRole = 2 // Роль User
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Сообщение об успехе на этой же странице
                ViewData["SuccessMessage"] = "Регистрация прошла успешно! Теперь вы можете войти в систему.";
                return View();
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Console.WriteLine($"Ошибка при регистрации: {ex.Message}");
                ModelState.AddModelError("", "Произошла ошибка при регистрации. Попробуйте еще раз.");
                return View();
            }
        }

        // GET: /Account/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // Проверяем авторизацию
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Получаем ID пользователя из сессии
            var userId = HttpContext.Session.GetInt32("UserId");

            // Получаем полные данные пользователя из базы
            var user = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .FirstOrDefaultAsync(u => u.IdUser == userId);

            if (user == null)
            {
                // Если пользователь не найден в базе, разлогиниваем
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            return View(user);
        }

        // Метод для проверки сложности пароля
        private PasswordValidationResult ValidatePassword(string password)
        {
            var result = new PasswordValidationResult { IsValid = true };

            // Проверка минимальной длины
            if (password.Length < 6)
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать минимум 6 символов";
                return result;
            }

            // Проверка на наличие цифр
            if (!Regex.IsMatch(password, @"\d"))
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать хотя бы одну цифру";
                return result;
            }

            // Проверка на наличие заглавных букв
            if (!Regex.IsMatch(password, @"[A-ZА-Я]"))
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать хотя бы одну заглавную букву";
                return result;
            }

            // Проверка на наличие строчных букв
            if (!Regex.IsMatch(password, @"[a-zа-я]"))
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать хотя бы одну строчную букву";
                return result;
            }

            // Проверка на наличие специальных символов
            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать хотя бы один специальный символ (!@#$%^&*()_+-=[]{};':\"|,.<>/? и т.д.)";
                return result;
            }

            return result;
        }

        // Метод для хэширования пароля
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Метод для проверки валидности email
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    // Вспомогательный класс для результата проверки пароля
    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}