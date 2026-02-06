// Controllers/FavoritesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using TripWise.Services;
using TripWise.Models;
using System.Text.Json;

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FavoritesController : Controller
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILogger<FavoritesController> _logger;

        public FavoritesController(IFavoriteService favoriteService, ILogger<FavoritesController> logger)
        {
            _favoriteService = favoriteService;
            _logger = logger;
        }

        // ==================== MVC ACTION ====================
        // GET: /Favorites - для отображения HTML страницы
        [HttpGet]
        [Route("/Favorites")]
        [Route("/Favorites/Index")]
        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Запрос страницы избранного");

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogDebug("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    return View(new List<FavoriteFlight>());
                }

                var favorites = await _favoriteService.GetUserFavoriteFlightsAsync(userId.Value);
                _logger.LogInformation("Найдено {Count} избранных рейсов", favorites.Count);

                return View(favorites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке страницы избранного");
                return View(new List<FavoriteFlight>());
            }
        }

        // ==================== API ACTIONS ====================
        [HttpPost("flights")]
        public async Task<IActionResult> AddFavoriteFlight([FromBody] AddFavoriteRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });
                }

                _logger.LogInformation("Добавление рейса в избранное. UserId: {UserId}, FlightId: {FlightId}",
                    userId.Value, request.FlightId);

                var favorite = new FavoriteFlight
                {
                    UserId = userId.Value,
                    FlightId = request.FlightId,
                    Airline = request.Airline,
                    AirlineCode = request.AirlineCode,
                    FlightNumber = request.FlightNumber,
                    DepartureCity = request.DepartureCity,
                    ArrivalCity = request.ArrivalCity,
                    DepartureAirport = request.DepartureAirport,
                    ArrivalAirport = request.ArrivalAirport,
                    DepartureTime = request.DepartureTime,
                    ArrivalTime = request.ArrivalTime,
                    Price = request.Price,
                    Currency = request.Currency ?? "RUB",
                    Transfers = request.Transfers,
                    Duration = request.Duration,
                    Aircraft = request.Aircraft,
                    IsReturn = request.IsReturn,
                    BookingUrl = request.BookingUrl,
                    SearchParameters = request.SearchParameters != null ?
                        JsonSerializer.Serialize(request.SearchParameters) : null,
                    AddedDate = DateTime.Now,
                    TripDate = request.TripDate
                };

                var result = await _favoriteService.AddFavoriteFlightAsync(favorite);

                if (result)
                {
                    _logger.LogInformation("Рейс добавлен в избранное. FlightId: {FlightId}", request.FlightId);
                    return Ok(new { success = true, message = "Рейс добавлен в избранное" });
                }
                else
                {
                    return Ok(new { success = false, message = "Рейс уже в избранном" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении рейса в избранное");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        [HttpDelete("flights/{flightId}")]
        public async Task<IActionResult> RemoveFavoriteFlight(string flightId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });
                }

                _logger.LogInformation("Удаление рейса из избранного. UserId: {UserId}, FlightId: {FlightId}",
                    userId.Value, flightId);

                var result = await _favoriteService.RemoveFavoriteFlightAsync(userId.Value, flightId);

                if (result)
                {
                    return Ok(new { success = true, message = "Рейс удален из избранного" });
                }
                else
                {
                    return NotFound(new { success = false, message = "Рейс не найден в избранном" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении рейса из избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        [HttpGet("flights")]
        public async Task<IActionResult> GetFavoriteFlights()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });
                }

                var favorites = await _favoriteService.GetUserFavoriteFlightsAsync(userId.Value);

                return Ok(new { success = true, favorites });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранных рейсов");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        [HttpGet("flights/check/{flightId}")]
        public async Task<IActionResult> CheckFavorite(string flightId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
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

                var isFavorite = await _favoriteService.IsFlightInFavoritesAsync(userId.Value, flightId);

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
                _logger.LogError(ex, "Ошибка при проверке избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
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
        public object SearchParameters { get; set; }
        public DateTime? TripDate { get; set; }
    }
}