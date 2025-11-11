using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TripWise.Models;

namespace TripWise.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}