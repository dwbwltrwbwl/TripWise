using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TripWise.Models;
using Microsoft.AspNetCore.Http;

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FavoritesController : ControllerBase
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<FavoritesController> _logger;

        public FavoritesController(TripWiseContext context, ILogger<FavoritesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: api/favorites/add
        [HttpPost("add")]
        public async Task<IActionResult> AddFavoriteFlight([FromBody] AddFavoriteRequest request)
        {
            try
            {
                _logger.LogInformation("========== ДОБАВЛЕНИЕ В ИЗБРАННОЕ ==========");
                _logger.LogInformation("Получен запрос на добавление: {@Request}", request);

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    _logger.LogWarning("Пользователь не авторизован");
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });
                }

                _logger.LogInformation("Добавление рейса в избранное. UserId: {UserId}, FlightId: {FlightId}",
                    userId.Value, request?.FlightId);

                if (request == null)
                {
                    _logger.LogWarning("Request is null");
                    return BadRequest(new { success = false, message = "Запрос не может быть пустым" });
                }

                if (string.IsNullOrEmpty(request.FlightId))
                {
                    _logger.LogWarning("FlightId is null or empty");
                    return BadRequest(new { success = false, message = "FlightId не может быть пустым" });
                }

                // Проверяем, не существует ли уже такой рейс
                _logger.LogInformation("Проверка существующего рейса...");
                var existing = await _context.FavoriteFlights
                    .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.FlightId == request.FlightId);

                if (existing != null)
                {
                    _logger.LogInformation("Рейс уже в избранном");
                    return Ok(new { success = false, message = "Рейс уже в избранном" });
                }

                var favorite = new FavoriteFlight
                {
                    UserId = userId.Value,
                    FlightId = request.FlightId,
                    Airline = request.Airline ?? "Авиакомпания",
                    AirlineCode = request.AirlineCode ?? "",
                    FlightNumber = request.FlightNumber ?? "",
                    DepartureCity = request.DepartureCity ?? "",
                    ArrivalCity = request.ArrivalCity ?? "",
                    DepartureAirport = request.DepartureAirport ?? "",
                    ArrivalAirport = request.ArrivalAirport ?? "",
                    DepartureTime = request.DepartureTime,
                    ArrivalTime = request.ArrivalTime,
                    Price = request.Price,
                    Currency = request.Currency ?? "RUB",
                    Transfers = request.Transfers,
                    Duration = request.Duration,
                    Aircraft = request.Aircraft ?? "",
                    IsReturn = request.IsReturn,
                    BookingUrl = request.BookingUrl ?? "",
                    AddedDate = DateTime.Now
                };

                _logger.LogInformation("Создан объект FavoriteFlight: {@Favorite}", favorite);

                _context.FavoriteFlights.Add(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Рейс успешно добавлен в избранное. FlightId: {FlightId}", request.FlightId);
                return Ok(new { success = true, message = "Рейс добавлен в избранное" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при добавлении рейса в избранное");
                return StatusCode(500, new { success = false, message = "Ошибка сервера: " + ex.Message });
            }
        }

        // POST: api/favorites/remove
        [HttpPost("remove")]
        public async Task<IActionResult> RemoveFavoriteFlight([FromBody] RemoveFavoriteRequest request)
        {
            try
            {
                _logger.LogInformation("========== УДАЛЕНИЕ ИЗ ИЗБРАННОГО ==========");
                _logger.LogInformation("Получен запрос на удаление: {@Request}", request);

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    _logger.LogWarning("Пользователь не авторизован");
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });
                }

                if (request == null || string.IsNullOrEmpty(request.FlightId))
                {
                    _logger.LogWarning("FlightId is null or empty");
                    return BadRequest(new { success = false, message = "FlightId не может быть пустым" });
                }

                _logger.LogInformation("Поиск рейса для удаления. UserId: {UserId}, FlightId: {FlightId}",
                    userId.Value, request.FlightId);

                var favorite = await _context.FavoriteFlights
                    .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.FlightId == request.FlightId);

                if (favorite == null)
                {
                    _logger.LogWarning("Рейс не найден в избранном");
                    return NotFound(new { success = false, message = "Рейс не найден в избранном" });
                }

                _context.FavoriteFlights.Remove(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Рейс успешно удален из избранного");
                return Ok(new { success = true, message = "Рейс удален из избранного" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при удалении рейса из избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера: " + ex.Message });
            }
        }

        // GET: api/favorites/check/{flightId}
        [HttpGet("check/{flightId}")]
        public async Task<IActionResult> CheckFavorite(string flightId)
        {
            try
            {
                _logger.LogInformation("========== ПРОВЕРКА ИЗБРАННОГО ==========");
                _logger.LogInformation("FlightId: {FlightId}", flightId);

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    return Ok(new
                    {
                        success = true,
                        isFavorite = false,
                        isAuthenticated = false,
                        message = "Пользователь не авторизован"
                    });
                }

                if (string.IsNullOrEmpty(flightId))
                {
                    return BadRequest(new { success = false, message = "FlightId не может быть пустым" });
                }

                var isFavorite = await _context.FavoriteFlights
                    .AnyAsync(f => f.UserId == userId.Value && f.FlightId == flightId);

                _logger.LogInformation("Результат проверки: {IsFavorite}", isFavorite);

                return Ok(new
                {
                    success = true,
                    isFavorite,
                    isAuthenticated = true,
                    message = isFavorite ? "Рейс в избранном" : "Рейс не в избранном"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при проверке избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // GET: api/favorites/list
        [HttpGet("list")]
        public async Task<IActionResult> GetFavoriteFlights()
        {
            try
            {
                _logger.LogInformation("========== ПОЛУЧЕНИЕ СПИСКА ИЗБРАННОГО ==========");

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    return Ok(new { success = true, favorites = new List<string>() });
                }

                var favorites = await _context.FavoriteFlights
                    .Where(f => f.UserId == userId.Value)
                    .Select(f => f.FlightId)
                    .ToListAsync();

                _logger.LogInformation("Найдено избранных рейсов: {Count}", favorites.Count);

                return Ok(new { success = true, favorites });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при получении списка избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }
        // Добавьте этот метод в FavoritesController для проверки
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new
            {
                message = "FavoritesController работает",
                timestamp = DateTime.Now,
                routes = new[] {
            "GET /api/favorites/test",
            "GET /api/favorites/list",
            "GET /api/favorites/check/{flightId}",
            "POST /api/favorites/add",
            "POST /api/favorites/remove"
        }
            });
        }
        [HttpGet("debug/{userId}")]
        public async Task<IActionResult> Debug(int userId)
        {
            try
            {
                _logger.LogInformation("=== DEBUG: Прямой запрос к БД для пользователя {UserId} ===", userId);

                // Прямой запрос к БД через контекст
                var favorites = await _context.FavoriteFlights
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.AddedDate)
                    .ToListAsync();

                _logger.LogInformation("DEBUG: Найдено {Count} рейсов", favorites.Count);

                foreach (var flight in favorites)
                {
                    _logger.LogInformation("DEBUG: {FlightId} - {Airline} {FlightNumber}",
                        flight.FlightId, flight.Airline, flight.FlightNumber);
                }

                return Ok(new
                {
                    success = true,
                    count = favorites.Count,
                    flights = favorites.Select(f => new
                    {
                        f.FlightId,
                        f.Airline,
                        f.FlightNumber,
                        f.DepartureCity,
                        f.ArrivalCity,
                        f.Price
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG ошибка");
                return Ok(new { success = false, error = ex.Message });
            }
        }
    }

    public class AddFavoriteRequest
    {
        public string FlightId { get; set; }
        public string Airline { get; set; }
        public string AirlineCode { get; set; }
        public string FlightNumber { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public int Transfers { get; set; }
        public int Duration { get; set; }
        public string Aircraft { get; set; }
        public bool IsReturn { get; set; }
        public string BookingUrl { get; set; }
    }

    public class RemoveFavoriteRequest
    {
        public string FlightId { get; set; }
    }
}