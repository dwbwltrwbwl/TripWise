// Controllers/FavoritesPageController.cs
using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using Microsoft.AspNetCore.Http;

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
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                // Показываем страницу без данных
                return View(new List<Models.FavoriteFlight>());
            }

            try
            {
                var favorites = await _favoriteService.GetUserFavoriteFlightsAsync(userId.Value);
                return View(favorites ?? new List<Models.FavoriteFlight>()); // Убедимся, что не возвращаем null
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранных рейсов");
                return View(new List<Models.FavoriteFlight>());
            }
        }
    }
}