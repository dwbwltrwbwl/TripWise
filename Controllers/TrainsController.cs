using Microsoft.AspNetCore.Mvc;
using TripWise.Models;
using TripWise.Services;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainsController : ControllerBase
    {
        private readonly RzdApiService _rzdApiService;
        private readonly ILogger<TrainsController> _logger;

        public TrainsController(RzdApiService rzdApiService, ILogger<TrainsController> logger)
        {
            _rzdApiService = rzdApiService;
            _logger = logger;
        }

        [HttpPost("search")]
        public async Task<IActionResult> SearchTrains([FromBody] TrainSearchRequest request)
        {
            try
            {
                _logger.LogInformation($"Поиск поездов: {request.DepartureStationId} -> {request.ArrivalStationId} на {request.DepartureDate}");

                var trains = await _rzdApiService.SearchTrains(request);

                return Ok(new
                {
                    success = true,
                    trains = trains
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске поездов");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Внутренняя ошибка сервера",
                    message = ex.Message
                });
            }
        }

        [HttpGet("stations")]
        public IActionResult GetStations()
        {
            var stations = new[]
            {
            new { id = "2000000", name = "Москва", region = "Москва" },
            new { id = "2004000", name = "Санкт-Петербург", region = "Санкт-Петербург" },
            new { id = "2078001", name = "Симферополь", region = "Крым" },
            new { id = "2064188", name = "Анапа", region = "Краснодарский край" },
            new { id = "2064130", name = "Сочи", region = "Краснодарский край" },
            new { id = "2060615", name = "Казань", region = "Татарстан" },
            new { id = "2064000", name = "Ростов-на-Дону", region = "Ростовская обл." },
            new { id = "2064788", name = "Краснодар", region = "Краснодарский край" },
            new { id = "2064110", name = "Новороссийск", region = "Краснодарский край" },
            new { id = "2014000", name = "Воронеж", region = "Воронежская обл." },
            new { id = "2060000", name = "Нижний Новгород", region = "Нижегородская обл." },
            new { id = "2064150", name = "Адлер", region = "Краснодарский край" },
            new { id = "2060001", name = "Нижний Новгород Моск.", region = "Нижегородская обл." },
            new { id = "2010000", name = "Ярославль", region = "Ярославская обл." },
            new { id = "2058000", name = "Калининград", region = "Калининградская обл." },
            new { id = "2060340", name = "Владимир", region = "Владимирская обл." }
        };

            return Ok(stations);
        }
    }
}
