using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TripWise.Models.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace TripWise.Controllers
{
    public class AccountController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<AccountController> _logger;
        private readonly IMemoryCache _cache;
        private const string DELETE_CODE_PREFIX = "DeleteCode_";

        public AccountController(TripWiseContext context, EmailService emailService,
            ILogger<AccountController> logger, IMemoryCache memoryCache)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cache = memoryCache;
        }

        [HttpGet]
        public async Task<IActionResult> TestEmail()
        {
            await _emailService.SendAsync(
                "tyumenelizaveta@yandex.ru",
                "Тест TripWise",
                "<b>SMTP Яндекс работает!</b>"
            );

            return Content("Письмо отправлено");
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            // Проверяем, есть ли сообщение об успешной регистрации
            if (TempData["RegistrationSuccess"] != null && (bool)TempData["RegistrationSuccess"])
            {
                ViewData["RegistrationSuccess"] = true;
                ViewData["RegisteredEmail"] = TempData["RegisteredEmail"];
            }

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
            // Сохраняем ВСЕ введенные значения для повторного отображения (ТОЛЬКО при ошибках)
            ViewData["FullName"] = fullName;
            ViewData["Email"] = email;
            ViewData["Password"] = password;
            ViewData["ConfirmPassword"] = confirmPassword;
            ViewData["AgreeTerms"] = agreeTerms;

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

                // УСПЕШНАЯ РЕГИСТРАЦИЯ - очищаем поля и показываем сообщение
                ViewData["SuccessMessage"] = "Регистрация прошла успешно! Теперь вы можете войти в систему.";
                ViewData["FullName"] = "";
                ViewData["Email"] = "";
                ViewData["Password"] = "";
                ViewData["ConfirmPassword"] = "";
                ViewData["AgreeTerms"] = "";

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

        // GET: /Account/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.IdUser == userId);
            if (user == null)
                return RedirectToAction("Login");

            var model = new EditProfileViewModel
            {
                Name = user.Name,
                Email = user.Email,
                Age = user.Age
            };

            return View(model);
        }

        // POST: /Account/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.IdUser == userId);
            if (user == null)
                return RedirectToAction("Login");

            user.Name = model.Name;
            user.Email = model.Email;
            user.Age = model.Age;

            await _context.SaveChangesAsync();

            return RedirectToAction("Profile");
        }

        // GET: /Account/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // GET: /Account/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .FirstOrDefaultAsync(u => u.IdUser == userId);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            // Количество поездок
            var trips = await _context.TripParticipants
                .Where(tp => tp.IdUser == userId)
                .Select(tp => tp.IdTrip)
                .Distinct()
                .ToListAsync();

            var tripCount = trips.Count;

            // Количество дней в поездках
            var travelDays = await _context.Trips
                .Where(t => trips.Contains(t.IdTrip))
                .Select(t => new { t.StartDate, t.EndDate })
                .ToListAsync();
            var totalTravelDays = travelDays.Sum(t =>
                (t.EndDate.Date - t.StartDate.Date).Days
            );

            // Количество групп (разные поездки = разные группы)
            var groupCount = tripCount;
            var totalShare = await _context.ExpenseShares
                .Where(es => es.IdUser == userId)
                .SumAsync(es => es.ShareAmount);

            var unpaidShare = await _context.ExpenseShares
                .Where(es => es.IdUser == userId && !es.IsPaid)
                .SumAsync(es => es.ShareAmount);

            ViewBag.TripCount = tripCount;
            ViewBag.TravelDays = totalTravelDays;
            ViewBag.GroupCount = groupCount;
            ViewBag.TotalShare = totalShare;
            ViewBag.UnpaidShare = unpaidShare;
            ViewBag.LastExpenses = await _context.Expenses
                .Where(e => e.PaidById == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View(user);
        }

        // GET: /Account/Delete
        [HttpGet]
        public IActionResult Delete()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var email = HttpContext.Session.GetString("UserEmail");

            var model = new DeleteAccountViewModel
            {
                Email = email,
                CodeSent = false
            };

            return View(model);
        }

        // POST: /Account/SendDeleteCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDeleteCode()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Json(new { success = false, message = "Сессия истекла" });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "Пользователь не найден" });

            try
            {
                // Генерируем 6-значный код
                var random = new Random();
                var code = random.Next(100000, 999999).ToString();

                // Сохраняем код в кэш на 15 минут
                var cacheKey = DELETE_CODE_PREFIX + userId;
                _cache.Set(cacheKey, code, TimeSpan.FromMinutes(15));

                // Отправляем код на email
                await _emailService.SendConfirmationCodeAsync(user.Email, code);

                _logger.LogInformation($"Код подтверждения отправлен пользователю {user.Email}");

                return Json(new { success = true, message = "Код подтверждения отправлен на ваш email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке кода подтверждения");
                return Json(new { success = false, message = "Ошибка при отправке кода" });
            }
        }

        // POST: /Account/ConfirmDelete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelete(string code)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Json(new { success = false, message = "Сессия истекла" });

            try
            {// Более гибкая проверка кода
                if (string.IsNullOrWhiteSpace(code))
                    return Json(new { success = false, message = "Введите код" });

                // Убираем все нецифровые символы
                code = new string(code.Where(char.IsDigit).ToArray());

                if (code.Length != 6)
                    return Json(new { success = false, message = "Код должен содержать 6 цифр" });

                // Проверяем код из кэша
                var cacheKey = DELETE_CODE_PREFIX + userId;
                if (!_cache.TryGetValue(cacheKey, out string cachedCode))
                    return Json(new { success = false, message = "Код истек или не был отправлен" });

                if (cachedCode != code)
                    return Json(new { success = false, message = "Неверный код подтверждения" });

                // Находим пользователя
                var user = await _context.Users
                    .Include(u => u.Expenses)
                    .Include(u => u.ExpenseShares)
                    .Include(u => u.TripParticipants)
                    .FirstOrDefaultAsync(u => u.IdUser == userId);

                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Начинаем транзакцию для безопасного удаления
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Удаляем доли расходов
                        _context.ExpenseShares.RemoveRange(user.ExpenseShares);

                        // 2. Удаляем участие в поездках
                        _context.TripParticipants.RemoveRange(user.TripParticipants);

                        // 3. Для расходов, которые оплатил пользователь, очищаем PaidById
                        var userExpenses = await _context.Expenses
                            .Where(e => e.PaidById == userId)
                            .ToListAsync();

                        foreach (var expense in userExpenses)
                        {
                            expense.PaidById = null;
                            _context.Expenses.Update(expense);
                        }

                        // 4. Удаляем самого пользователя
                        _context.Users.Remove(user);

                        // 5. Сохраняем изменения
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // 6. Очищаем сессию и кэш
                        HttpContext.Session.Clear();
                        Response.Cookies.Delete("UserEmail");
                        _cache.Remove(cacheKey);

                        _logger.LogInformation($"Аккаунт пользователя {user.Email} успешно удален");

                        return Json(new
                        {
                            success = true,
                            message = "Аккаунт успешно удален",
                            redirectUrl = Url.Action("Index", "Home")
                        });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Ошибка при удалении аккаунта");
                        return Json(new { success = false, message = "Ошибка при удалении аккаунта" });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подтверждении удаления");
                return Json(new { success = false, message = "Произошла ошибка" });
            }
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