using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TripWise.Models;
using TripWise.Models.ViewModels;

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
        [HttpGet("Users")]
        public async Task<IActionResult> Users()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            // Получаем всех пользователей с их ролями
            var users = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }
        [HttpGet("GetUserStats/{userId}")]
        public async Task<IActionResult> GetUserStats(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            // Получаем статистику пользователя
            var trips = await _context.TripParticipants
                .Where(tp => tp.IdUser == userId)
                .Select(tp => tp.IdTrip)
                .Distinct()
                .CountAsync();

            var expenses = await _context.ExpenseShares
                .Where(es => es.IdUser == userId)
                .SumAsync(es => es.ShareAmount);

            var documents = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .CountAsync();

            var reviews = await _context.Reviews
                .Where(r => r.UserId == userId)
                .CountAsync();

            var viewModel = new
            {
                UserName = $"{user.LastName} {user.FirstName} {user.MiddleName}",
                UserEmail = user.Email,
                RegisteredAt = user.CreatedAt,
                TripsCount = trips,
                TotalExpenses = expenses,
                DocumentsCount = documents,
                ReviewsCount = reviews,
                HasAvatar = !string.IsNullOrEmpty(user.AvatarPath),
                AvatarPath = user.AvatarPath
            };

            return PartialView("_UserStats", viewModel);
        }

        [HttpPost("DeleteUser/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Нельзя удалить самого себя
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (currentUserId == userId)
                    return Json(new { success = false, message = "Нельзя удалить свой собственный аккаунт" });

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("ToggleUserRole/{userId}")]
        public async Task<IActionResult> ToggleUserRole(int userId, [FromBody] dynamic data)
        {
            try
            {
                int newRole = data.newRole;

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                user.IdRole = newRole;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpGet("Analytics")]
        public async Task<IActionResult> Analytics()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            var model = new AnalyticsViewModel();
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);
            var monthAgo = today.AddMonths(-1);

            try
            {
                // ========== ПОЛЬЗОВАТЕЛИ ==========
                model.TotalUsers = await _context.Users.CountAsync();

                model.NewUsersToday = await _context.Users
                    .CountAsync(u => u.CreatedAt.Date == today);

                model.NewUsersWeek = await _context.Users
                    .CountAsync(u => u.CreatedAt >= weekAgo);

                model.NewUsersMonth = await _context.Users
                    .CountAsync(u => u.CreatedAt >= monthAgo);

                // ========== АВИАБИЛЕТЫ ==========
                model.TotalFlightBookings = await _context.FlightBookings.CountAsync();

                // Для подсчета дохода берем только подтвержденные бронирования
                // Предполагаем, что Status = 1 означает "Подтвержден"
                var confirmedFlightBookings = await _context.FlightBookings
                    .Where(f => f.Status == BookingStatus.Confirmed) // Или используйте нужное значение enum
                    .ToListAsync();
                model.FlightRevenue = confirmedFlightBookings.Sum(f => f.Price);

                // ========== ЖД БИЛЕТЫ ==========
                model.TotalTrainBookings = await _context.TrainOrders.CountAsync();

                var confirmedTrainOrders = await _context.TrainOrders
                    .Where(t => t.Status == OrderStatus.Confirmed) // Или используйте нужное значение enum
                    .ToListAsync();
                model.TrainRevenue = confirmedTrainOrders.Sum(t => t.TotalPrice);

                // ========== ОТЕЛИ ==========
                model.TotalHotelBookings = await _context.HotelBookings.CountAsync();

                var confirmedHotelBookings = await _context.HotelBookings
                    .Where(h => h.Status == BookingStatus.Confirmed) // Или используйте нужное значение enum
                    .ToListAsync();
                model.HotelRevenue = confirmedHotelBookings.Sum(h => h.TotalPrice);

                // Общий оборот
                model.TotalRevenue = model.FlightRevenue + model.TrainRevenue + model.HotelRevenue;

                // ========== ОТЗЫВЫ ==========
                var reviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .ToListAsync();

                model.TotalReviews = reviews.Count;
                model.AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

                // Последние отзывы
                model.RecentReviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(4)
                    .Select(r => new RecentReview
                    {
                        UserName = r.Name,
                        Rating = r.Rating,
                        RatingStars = GetStars(r.Rating),
                        CreatedAt = r.CreatedAt
                    })
                    .ToListAsync();

                // ========== АКТИВНОСТЬ ПОЛЬЗОВАТЕЛЕЙ ==========
                var lastDay = DateTime.UtcNow.AddDays(-1);
                var lastWeek = DateTime.UtcNow.AddDays(-7);
                var lastMonth = DateTime.UtcNow.AddMonths(-1);

                // Активные пользователи за день
                var activeUserIds = new HashSet<int>();

                var flightUsers = await _context.FlightBookings
                    .Where(f => f.CreatedAt >= lastDay)
                    .Select(f => f.UserId)
                    .ToListAsync();

                var trainUsers = await _context.TrainOrders
                    .Where(t => t.CreatedAt >= lastDay)
                    .Select(t => t.UserId)
                    .ToListAsync();

                var hotelUsers = await _context.HotelBookings
                    .Where(h => h.CreatedAt >= lastDay)
                    .Select(h => h.UserId)
                    .ToListAsync();

                var reviewUsers = await _context.Reviews
                    .Where(r => r.CreatedAt >= lastDay)
                    .Select(r => r.UserId)
                    .ToListAsync();

                foreach (var id in flightUsers) activeUserIds.Add(id);
                foreach (var id in trainUsers) activeUserIds.Add(id);
                foreach (var id in hotelUsers) activeUserIds.Add(id);
                foreach (var id in reviewUsers) activeUserIds.Add(id);

                model.ActiveUsersToday = activeUserIds.Count;

                // Активные пользователи за неделю
                activeUserIds.Clear();
                flightUsers = await _context.FlightBookings
                    .Where(f => f.CreatedAt >= lastWeek)
                    .Select(f => f.UserId)
                    .ToListAsync();
                foreach (var id in flightUsers) activeUserIds.Add(id);

                trainUsers = await _context.TrainOrders
                    .Where(t => t.CreatedAt >= lastWeek)
                    .Select(t => t.UserId)
                    .ToListAsync();
                foreach (var id in trainUsers) activeUserIds.Add(id);

                hotelUsers = await _context.HotelBookings
                    .Where(h => h.CreatedAt >= lastWeek)
                    .Select(h => h.UserId)
                    .ToListAsync();
                foreach (var id in hotelUsers) activeUserIds.Add(id);

                reviewUsers = await _context.Reviews
                    .Where(r => r.CreatedAt >= lastWeek)
                    .Select(r => r.UserId)
                    .ToListAsync();
                foreach (var id in reviewUsers) activeUserIds.Add(id);

                model.ActiveUsersWeek = activeUserIds.Count;

                // Активные пользователи за месяц
                activeUserIds.Clear();
                flightUsers = await _context.FlightBookings
                    .Where(f => f.CreatedAt >= lastMonth)
                    .Select(f => f.UserId)
                    .ToListAsync();
                foreach (var id in flightUsers) activeUserIds.Add(id);

                trainUsers = await _context.TrainOrders
                    .Where(t => t.CreatedAt >= lastMonth)
                    .Select(t => t.UserId)
                    .ToListAsync();
                foreach (var id in trainUsers) activeUserIds.Add(id);

                hotelUsers = await _context.HotelBookings
                    .Where(h => h.CreatedAt >= lastMonth)
                    .Select(h => h.UserId)
                    .ToListAsync();
                foreach (var id in hotelUsers) activeUserIds.Add(id);

                reviewUsers = await _context.Reviews
                    .Where(r => r.CreatedAt >= lastMonth)
                    .Select(r => r.UserId)
                    .ToListAsync();
                foreach (var id in reviewUsers) activeUserIds.Add(id);

                model.ActiveUsersMonth = activeUserIds.Count;

                // ========== ГРАФИК АКТИВНОСТИ ПОЛЬЗОВАТЕЛЕЙ (7 дней) ==========
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var dayStart = date;
                    var dayEnd = date.AddDays(1);

                    // Считаем новых пользователей за этот день
                    var newUsers = await _context.Users
                        .CountAsync(u => u.CreatedAt >= dayStart && u.CreatedAt < dayEnd);

                    // Считаем бронирования за этот день
                    var flightBookings = await _context.FlightBookings
                        .CountAsync(f => f.CreatedAt >= dayStart && f.CreatedAt < dayEnd);

                    var trainBookings = await _context.TrainOrders
                        .CountAsync(t => t.CreatedAt >= dayStart && t.CreatedAt < dayEnd);

                    var hotelBookings = await _context.HotelBookings
                        .CountAsync(h => h.CreatedAt >= dayStart && h.CreatedAt < dayEnd);

                    var totalActivity = newUsers + flightBookings + trainBookings + hotelBookings;

                    model.UserActivity.Add(new ChartDataPoint
                    {
                        Label = date.ToString("dd MMM", new System.Globalization.CultureInfo("ru-RU")),
                        Value = totalActivity
                    });
                }

                // ========== ДОХОД ПО МЕСЯЦАМ (текущий год) ==========
                var currentYear = DateTime.UtcNow.Year;

                for (int month = 1; month <= 12; month++)
                {
                    var monthStart = new DateTime(currentYear, month, 1);
                    var monthEnd = monthStart.AddMonths(1);

                    // Авиа доход за месяц
                    var flightBookingsMonth = await _context.FlightBookings
                        .Where(f => f.CreatedAt >= monthStart && f.CreatedAt < monthEnd && f.Status == BookingStatus.Confirmed)
                        .ToListAsync();
                    var flightRevenue = flightBookingsMonth.Sum(f => f.Price);

                    // ЖД доход за месяц
                    var trainOrdersMonth = await _context.TrainOrders
                        .Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd && t.Status == OrderStatus.Confirmed)
                        .ToListAsync();
                    var trainRevenue = trainOrdersMonth.Sum(t => t.TotalPrice);

                    // Отели доход за месяц
                    var hotelBookingsMonth = await _context.HotelBookings
                        .Where(h => h.CreatedAt >= monthStart && h.CreatedAt < monthEnd && h.Status == BookingStatus.Confirmed)
                        .ToListAsync();
                    var hotelRevenue = hotelBookingsMonth.Sum(h => h.TotalPrice);

                    var totalRevenue = flightRevenue + trainRevenue + hotelRevenue;

                    model.MonthlyRevenue.Add(new ChartDataPoint
                    {
                        Label = monthStart.ToString("MMM", new System.Globalization.CultureInfo("ru-RU")),
                        Amount = totalRevenue / 1000 // в тысячах рублей
                    });
                }

                // ========== ПОПУЛЯРНЫЕ НАПРАВЛЕНИЯ ==========
                var destinations = new List<PopularDestination>();

                // Авиа направления
                var flightRoutes = await _context.FlightBookings
                    .GroupBy(f => new { f.DepartureCity, f.ArrivalCity })
                    .Select(g => new
                    {
                        Route = $"{g.Key.DepartureCity} → {g.Key.ArrivalCity}",
                        Count = g.Count(),
                        Type = "Авиа"
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                foreach (var route in flightRoutes)
                {
                    destinations.Add(new PopularDestination
                    {
                        Route = route.Route,
                        Type = route.Type,
                        Count = route.Count,
                        Icon = "fa-plane",
                        Color = "primary"
                    });
                }

                // ЖД направления
                var trainRoutes = await _context.TrainOrders
                    .GroupBy(t => new { t.DepartureStationName, t.ArrivalStationName })
                    .Select(g => new
                    {
                        Route = $"{g.Key.DepartureStationName} → {g.Key.ArrivalStationName}",
                        Count = g.Count(),
                        Type = "ЖД"
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                foreach (var route in trainRoutes)
                {
                    destinations.Add(new PopularDestination
                    {
                        Route = route.Route,
                        Type = route.Type,
                        Count = route.Count,
                        Icon = "fa-train",
                        Color = "success"
                    });
                }

                // Отели
                var hotels = await _context.HotelBookings
                    .GroupBy(h => h.HotelName)
                    .Select(g => new
                    {
                        Route = g.Key,
                        Count = g.Count(),
                        Type = "Отель"
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                foreach (var hotel in hotels)
                {
                    destinations.Add(new PopularDestination
                    {
                        Route = hotel.Route,
                        Type = hotel.Type,
                        Count = hotel.Count,
                        Icon = "fa-hotel",
                        Color = "warning"
                    });
                }

                // Сортируем по популярности и берем топ-5
                model.PopularDestinations = destinations
                    .OrderByDescending(d => d.Count)
                    .Take(5)
                    .ToList();

                // Вычисляем проценты
                if (model.PopularDestinations.Any())
                {
                    var maxCount = model.PopularDestinations.Max(d => d.Count);
                    foreach (var dest in model.PopularDestinations)
                    {
                        dest.Percentage = maxCount > 0 ? (int)((double)dest.Count / maxCount * 100) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Используем ILogger для логирования ошибки
                // Если у вас нет ILogger, можно использовать Console.WriteLine или временно убрать
                Console.WriteLine($"Ошибка при загрузке аналитики: {ex.Message}");
            }

            return View(model);
        }

        // Вспомогательный метод для отображения звезд
        private string GetStars(int rating)
        {
            return string.Concat(Enumerable.Repeat("★", rating)) +
                   string.Concat(Enumerable.Repeat("☆", 5 - rating));
        }

        [HttpGet("Settings")]
        public IActionResult Settings()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            // Загрузить текущие настройки из БД (если есть таблица Settings)
            return View();
        }
    }
}