using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TripWise.Models.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace TripWise.Controllers
{
    public class AccountController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<AccountController> _logger;
        private readonly IMemoryCache _cache;
        private const string DELETE_CODE_PREFIX = "DeleteCode_";
        private const string PASSWORD_CHANGE_CODE_PREFIX = "PasswordChangeCode_";
        private const string PASSWORD_CHANGE_DATA_PREFIX = "PasswordChangeData_";

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
                    HttpContext.Session.SetString("UserName", $"{user.LastName} {user.FirstName}");
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetInt32("UserRole", user.IdRole);

                    // РЕАЛЬНАЯ РАБОТА ЗАПОМНИТЬ МЕНЯ
                    // Если пользователь отметил "Запомнить меня"
                    bool remember = !string.IsNullOrEmpty(rememberMe) && rememberMe == "true";

                    if (remember)
                    {
                        // Создаем токен
                        var authToken = GenerateAuthToken(user.IdUser, user.Email);

                        // Сохраняем в БД
                        await SaveAuthToken(user.IdUser, authToken);

                        // Устанавливаем куки
                        var cookieOptions = new CookieOptions
                        {
                            Expires = DateTime.Now.AddDays(30),
                            HttpOnly = true,
                            IsEssential = true,
                            SameSite = SameSiteMode.Lax  // Добавьте это
                        };

                        Response.Cookies.Append("AuthToken", authToken, cookieOptions);
                        Response.Cookies.Append("RememberMe", "true", cookieOptions);
                        Response.Cookies.Append("UserEmail", user.Email, cookieOptions); // ← ЭТО ВАЖНО!
                    }
                    else
                    {
                        // Если не "запомнить", то только сессия
                        // Удаляем старые куки если есть
                        Response.Cookies.Delete("AuthToken");
                        Response.Cookies.Delete("RememberMe");
                    }
                    // ⬇️⬇️⬇️ ВСТАВЬТЕ ЭТОТ КОД ЗДЕСЬ ⬇️⬇️⬇️
                    // Добавляем стандартную аутентификацию ASP.NET Core
                    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.IdUser.ToString()),
        new Claim(ClaimTypes.Name, user.Email),
        new Claim(ClaimTypes.Role, user.IdRole.ToString())
    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = remember,
                        ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);
                    // ⬆️⬆️⬆️ КОНЕЦ ВСТАВКИ ⬆️⬆️⬆️

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
                _logger.LogError(ex, "Ошибка при авторизации пользователя {Email}", email);
                ModelState.AddModelError("", "Произошла ошибка при авторизации. Попробуйте еще раз.");
                return View();
            }
        }
        private string GenerateAuthToken(int userId, string email)
        {
            // Используем GUID + timestamp для уникальности
            var tokenData = $"{userId}|{email}|{DateTime.UtcNow.Ticks}|{Guid.NewGuid()}";
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(tokenData);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private async Task SaveAuthToken(int userId, string token)
        {
            var authToken = new UserAuthToken
            {
                UserId = userId,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            // Удаляем старые токены этого пользователя
            var oldTokens = await _context.UserAuthTokens
                .Where(t => t.UserId == userId && t.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (oldTokens.Any())
            {
                _context.UserAuthTokens.RemoveRange(oldTokens);
            }

            _context.UserAuthTokens.Add(authToken);
            await _context.SaveChangesAsync();
        }

        private async Task<bool> ValidateAuthToken(int userId, string token)
        {
            // Ищем валидный токен в базе
            var authToken = await _context.UserAuthTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == userId &&
                    t.Token == token &&
                    t.ExpiresAt > DateTime.UtcNow);

            return authToken != null;
        }
        private async Task DeleteAuthToken(int userId, string token)
        {
            var authToken = await _context.UserAuthTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);

            if (authToken != null)
            {
                _context.UserAuthTokens.Remove(authToken);
                await _context.SaveChangesAsync();
            }
        }

        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var authToken = Request.Cookies["AuthToken"];

            if (userId.HasValue && !string.IsNullOrEmpty(authToken))
            {
                // Удаляем токен из БД
                var token = await _context.UserAuthTokens
                    .FirstOrDefaultAsync(t => t.UserId == userId.Value && t.Token == authToken);
                if (token != null)
                {
                    _context.UserAuthTokens.Remove(token);
                    await _context.SaveChangesAsync();
                }
            }

            // Очищаем сессию
            HttpContext.Session.Clear();

            // Удаляем ВСЕ куки
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = true
            };

            Response.Cookies.Append("AuthToken", "", cookieOptions);
            Response.Cookies.Append("RememberMe", "", cookieOptions);
            Response.Cookies.Append("UserEmail", "", cookieOptions);

            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        [Route("Account/CleanupExpiredTokens")]
        public async Task<IActionResult> CleanupExpiredTokens()
        {
            var expiredTokens = await _context.UserAuthTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            _context.UserAuthTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();

            return Content($"Удалено {expiredTokens.Count} устаревших токенов");
        }
        [HttpGet]
        public async Task<IActionResult> DebugAuth()
        {
            var result = new
            {
                SessionUserId = HttpContext.Session.GetInt32("UserId"),
                Cookies = new
                {
                    AuthToken = Request.Cookies["AuthToken"],
                    RememberMe = Request.Cookies["RememberMe"],
                    UserEmail = Request.Cookies["UserEmail"]
                },
                DatabaseTokens = await _context.UserAuthTokens.ToListAsync()
            };

            return Json(result);
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(string lastName, string firstName, string middleName, string email, string password, string confirmPassword, string agreeTerms)
        {
            ViewData["LastName"] = lastName;    
            ViewData["FirstName"] = firstName; 
            ViewData["MiddleName"] = middleName;
            ViewData["Email"] = email;
            ViewData["Password"] = password;
            ViewData["ConfirmPassword"] = confirmPassword;
            ViewData["AgreeTerms"] = agreeTerms;

            // Валидация
            if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName) ||
        string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
        string.IsNullOrEmpty(confirmPassword))
            {
                ModelState.AddModelError("", "Все поля, кроме отчества, обязательны для заполнения");
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
                    LastName = lastName,      // ← изменено
                    FirstName = firstName,    // ← добавлено
                    MiddleName = middleName,  // ← добавлено (может быть null)
                    Email = email,
                    PasswordHash = HashPassword(password),
                    Age = null,
                    CreatedAt = DateTime.UtcNow,
                    IdRole = 2 // Роль User
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // УСПЕШНАЯ РЕГИСТРАЦИЯ - очищаем поля
                ViewData["SuccessMessage"] = "Регистрация прошла успешно! Теперь вы можете войти в систему.";
                ViewData["LastName"] = "";      // ← очистка
                ViewData["FirstName"] = "";     // ← очистка
                ViewData["MiddleName"] = "";    // ← очистка
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
                LastName = user.LastName,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
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

            user.LastName = model.LastName;
            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
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
            // В методе Profile добавьте:
            ViewBag.DocumentCount = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .CountAsync();

            ViewBag.FolderCount = await _context.DocumentFolders
                .Where(f => f.UserId == userId)
                .CountAsync();

            ViewBag.RecentDocuments = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .Include(d => d.Folder)
                .OrderByDescending(d => d.CreatedAt)
                .Take(3)
                .Select(d => new {
                    d.Name,
                    d.FileType,
                    d.FileSize,
                    d.CreatedAt,
                    FolderName = d.Folder != null ? d.Folder.Name : null
                })
                .ToListAsync();

            ViewBag.UserFolders = await _context.DocumentFolders
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.Name)
                .Select(f => new {
                    f.IdFolder,
                    f.Name
                })
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCurrentPassword([FromBody] VerifyCurrentPasswordRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Проверяем текущий пароль
                var hashedPassword = HashPassword(request.CurrentPassword);
                if (user.PasswordHash != hashedPassword)
                    return Json(new { success = false, message = "Неверный текущий пароль" });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке текущего пароля");
                return Json(new { success = false, message = "Произошла ошибка" });
            }
        }

        // POST: /Account/SendPasswordChangeCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPasswordChangeCode([FromBody] PasswordChangeRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Проверяем новый пароль
                var passwordValidation = ValidatePassword(request.NewPassword);
                if (!passwordValidation.IsValid)
                    return Json(new { success = false, message = passwordValidation.ErrorMessage });

                // Генерируем 6-значный код
                var random = new Random();
                var code = random.Next(100000, 999999).ToString();

                // Сохраняем код в кэш на 15 минут
                var codeCacheKey = PASSWORD_CHANGE_CODE_PREFIX + userId;
                _cache.Set(codeCacheKey, code, TimeSpan.FromMinutes(15));

                // Сохраняем данные для смены пароля (новый пароль) на 15 минут
                var dataCacheKey = PASSWORD_CHANGE_DATA_PREFIX + userId;
                var passwordData = new PasswordChangeData
                {
                    NewPassword = request.NewPassword,
                    Timestamp = DateTime.UtcNow
                };
                _cache.Set(dataCacheKey, passwordData, TimeSpan.FromMinutes(15));

                // Отправляем код на email
                await _emailService.SendPasswordChangeCodeAsync(user.Email, code);

                _logger.LogInformation($"Код подтверждения смены пароля отправлен пользователю {user.Email}");

                return Json(new { success = true, message = "Код подтверждения отправлен на ваш email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке кода подтверждения смены пароля");
                return Json(new { success = false, message = "Ошибка при отправке кода" });
            }
        }
        // GET: /Account/MyDocuments
        [Route("Account/MyDocuments")]
        public IActionResult MyDocuments()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            return View(); // Ищет Views/Account/MyDocuments.cshtml
        }

        // POST: /Account/ChangePasswordWithVerification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordWithVerification([FromBody] VerifyPasswordChangeRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                // Убираем все нецифровые символы
                var code = new string(request.VerificationCode.Where(char.IsDigit).ToArray());

                if (code.Length != 6)
                    return Json(new { success = false, message = "Код должен содержать 6 цифр" });

                // Проверяем код из кэша
                var codeCacheKey = PASSWORD_CHANGE_CODE_PREFIX + userId;
                if (!_cache.TryGetValue(codeCacheKey, out string cachedCode))
                    return Json(new { success = false, message = "Код истек или не был отправлен" });

                if (cachedCode != code)
                    return Json(new { success = false, message = "Неверный код подтверждения" });

                // Получаем данные для смены пароля
                var dataCacheKey = PASSWORD_CHANGE_DATA_PREFIX + userId;
                if (!_cache.TryGetValue(dataCacheKey, out PasswordChangeData passwordData))
                    return Json(new { success = false, message = "Данные для смены пароля устарели" });

                // Находим пользователя
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Обновляем пароль
                user.PasswordHash = HashPassword(passwordData.NewPassword);

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Очищаем кэш
                _cache.Remove(codeCacheKey);
                _cache.Remove(dataCacheKey);

                // Отправляем уведомление об успешной смене пароля
                await SendPasswordChangeSuccessEmail(user.Email);

                _logger.LogInformation($"Пароль пользователя {user.Email} успешно изменен");

                return Json(new
                {
                    success = true,
                    message = "Пароль успешно изменен",
                    redirectUrl = Url.Action("Profile", "Account")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при смене пароля");
                return Json(new { success = false, message = "Произошла ошибка при смене пароля" });
            }
        }
        private async Task SendPasswordChangeSuccessEmail(string toEmail)
        {
            var subject = "Пароль успешно изменен - Вместе В Путь";
            var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='background: #d4edda; border: 1px solid #c3e6cb; border-radius: 10px; padding: 20px; margin-bottom: 20px;'>
                    <h2 style='color: #155724; margin-top: 0;'>
                        <i class='fas fa-check-circle'></i> Пароль успешно изменен
                    </h2>
                    <p style='color: #155724;'>
                        Пароль для вашего аккаунта в <strong>Вместе В Путь</strong> был успешно изменен.
                    </p>
                </div>
                
                <div style='background: #fff3cd; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
                    <p style='margin: 0; color: #856404;'>
                        <strong><i class='fas fa-exclamation-triangle'></i> Важно!</strong><br>
                        Если вы не меняли пароль, немедленно свяжитесь с нашей службой поддержки.
                    </p>
                </div>
                
                <div style='border-top: 1px solid #eee; padding-top: 20px; margin-top: 30px;'>
                    <p style='color: #888; font-size: 14px;'>
                        Для дополнительной безопасности рекомендуется:<br>
                        1. Использовать уникальный пароль для каждого сервиса<br>
                        2. Включить двухфакторную аутентификацию (если доступно)<br>
                        3. Регулярно обновлять пароль
                    </p>
                </div>
                
                <div style='text-align: center; margin-top: 30px;'>
                    <p style='color: #aaa; font-size: 12px;'>
                        С уважением, команда <strong>Вместе В Путь</strong><br>
                        {DateTime.Now.Year} © Все права защищены
                    </p>
                </div>
            </div>";

            await _emailService.SendAsync(toEmail, subject, body);
        
    }
}

    // Вспомогательный класс для результата проверки пароля
    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    public class VerifyCurrentPasswordRequest
    {
        public string CurrentPassword { get; set; }
    }

    public class PasswordChangeRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class VerifyPasswordChangeRequest
    {
        public string VerificationCode { get; set; }
    }

    public class PasswordChangeData
    {
        public string NewPassword { get; set; }
        public DateTime Timestamp { get; set; }
    }

}