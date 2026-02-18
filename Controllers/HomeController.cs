using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.ViewModels;
using System.Diagnostics;

namespace TripWise.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly TripWiseContext _context;

    public HomeController(ILogger<HomeController> logger, TripWiseContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel();

        try
        {
            // Получаем последние 6 одобренных отзывов и преобразуем в HomeReviewDto
            var recentReviews = await _context.Reviews
                .Where(r => !r.IsDeleted && r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .Take(6)
                .Select(r => new HomeReviewDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Rating = r.Rating,
                    Text = r.Text,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            model.RecentReviews = recentReviews;
            var allReviews = await _context.Reviews
                .Where(r => !r.IsDeleted && r.IsApproved)
                .ToListAsync();

            if (allReviews.Any())
            {
                model.Statistics = new ReviewStatisticsDto
                {
                    TotalReviews = allReviews.Count,
                    AverageRating = Math.Round(allReviews.Average(r => r.Rating), 1),
                    RatingCounts = new Dictionary<int, int>
                {
                    { 5, allReviews.Count(r => r.Rating == 5) },
                    { 4, allReviews.Count(r => r.Rating == 4) },
                    { 3, allReviews.Count(r => r.Rating == 3) },
                    { 2, allReviews.Count(r => r.Rating == 2) },
                    { 1, allReviews.Count(r => r.Rating == 1) }
                }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке отзывов на главную страницу");
        }

        return View(model);
    }

    public IActionResult Flights()
    {
        return View();
    }

    public IActionResult Railway()
    {
        return View();
    }

    public IActionResult Hotels()
    {
        return View();
    }

    public IActionResult Trips()
    {
        return View();
    }

    public IActionResult Groups()
    {
        return View();
    }

    public IActionResult Budget()
    {
        return View();
    }

    public IActionResult Activities()
    {
        return View();
    }

    public IActionResult Favorites()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terms()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Partners()
    {
        return View();
    }

    public IActionResult Help()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult FAQ()
    {
        return View();
    }

    public IActionResult Reviews()
    {
        return View();
    }

    public IActionResult Chats()
    {
        // Проверяем авторизацию пользователя
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Пользователь не авторизован, но показываем страницу
            // В реальном приложении можно редиректить на логин
        }

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}