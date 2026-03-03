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

                // Проверяем, не существует ли уже такой рейс
                var existing = await _context.FavoriteFlights
                    .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.FlightId == request.FlightId);

                if (existing != null)
                {
                    // Если рейс уже есть, возвращаем сообщение об этом
                    _logger.LogInformation("Рейс уже в избранном");
                    return Ok(new { success = false, message = "Рейс уже в избранном" });
                }

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

                _context.FavoriteFlights.Add(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Рейс добавлен в избранное. FlightId: {FlightId}", request.FlightId);
                return Ok(new { success = true, message = "Рейс добавлен в избранное" });
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

                var favorite = await _context.FavoriteFlights
                    .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.FlightId == flightId);

                if (favorite == null)
                {
                    return NotFound(new { success = false, message = "Рейс не найден в избранном" });
                }

                _context.FavoriteFlights.Remove(favorite);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Рейс удален из избранного" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении рейса из избранного");
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

                var isFavorite = await _context.FavoriteFlights
                    .AnyAsync(f => f.UserId == userId.Value && f.FlightId == flightId);

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

                var favorites = await _context.FavoriteFlights
                    .Where(f => f.UserId == userId.Value)
                    .OrderByDescending(f => f.AddedDate)
                    .ToListAsync();

                return Ok(new { success = true, favorites });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранных рейсов");
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