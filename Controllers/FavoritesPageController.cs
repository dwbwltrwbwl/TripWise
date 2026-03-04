using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using Microsoft.AspNetCore.Http;
using TripWise.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TripWise.Controllers
{
    public class FavoritesPageController : Controller
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILogger<FavoritesPageController> _logger;

        public FavoritesPageController(IFavoriteService favoriteService, ILogger<FavoritesPageController> logger)
        {
            _favoriteService = favoriteService;
            _logger = logger;
        }

        [HttpGet]
        [Route("Favorites")]
        [Route("Home/Favorites")]
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            _logger.LogInformation("========== СТРАНИЦА ИЗБРАННОГО ==========");
            _logger.LogInformation("UserId из сессии: {UserId}", userId);
            _logger.LogInformation("Путь запроса: {Path}", Request.Path);

            if (!userId.HasValue)
            {
                _logger.LogWarning("Пользователь не авторизован");
                return View("~/Views/Home/Favorites.cshtml", new List<FavoriteFlight>());
            }

            try
            {
                _logger.LogInformation("Вызов FavoriteService.GetUserFavoriteFlightsAsync для пользователя {UserId}", userId.Value);

                var favorites = await _favoriteService.GetUserFavoriteFlightsAsync(userId.Value);

                _logger.LogInformation("Сервис вернул {Count} рейсов", favorites?.Count ?? 0);

                // Проверим каждый рейс
                if (favorites != null && favorites.Count > 0)
                {
                    for (int i = 0; i < favorites.Count; i++)
                    {
                        var flight = favorites[i];
                        _logger.LogInformation("Рейс {0}: FlightId={1}, Airline={2}, FlightNumber={3}, DepartureCity={4}, ArrivalCity={5}, Price={6}",
                            i + 1,
                            flight.FlightId ?? "null",
                            flight.Airline ?? "null",
                            flight.FlightNumber ?? "null",
                            flight.DepartureCity ?? "null",
                            flight.ArrivalCity ?? "null",
                            flight.Price);
                    }

                    _logger.LogInformation("Всего рейсов в модели: {Count}", favorites.Count);
                }
                else
                {
                    _logger.LogWarning("Сервис вернул пустой список или null");
                }

                // Явно создаем список, даже если favorites null
                var model = favorites ?? new List<FavoriteFlight>();
                _logger.LogInformation("Передаем в представление модель с {Count} рейсами", model.Count);

                return View("~/Views/Home/Favorites.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при получении избранных рейсов для пользователя {UserId}", userId.Value);
                return View("~/Views/Home/Favorites.cshtml", new List<FavoriteFlight>());
            }
        }
    }
}