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

        [HttpGet("stations/search")]
        public IActionResult SearchStations([FromQuery] string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2)
            {
                return Ok(new List<object>());
            }

            var allStations = GetAllStations();
            var lowerQuery = query.ToLower();

            var results = allStations
                .Where(s => s.Name.ToLower().Contains(lowerQuery) ||
                           s.Region.ToLower().Contains(lowerQuery))
                .Take(10)
                .Select(s => new { id = s.Id, name = s.Name, region = s.Region })
                .ToList();

            return Ok(results);
        }

        [HttpGet("stations")]
        public IActionResult GetAllStations()
        {
            var stations = GetAllStations()
                .Select(s => new { id = s.Id, name = s.Name, region = s.Region })
                .ToList();

            return Ok(stations);
        }

        private List<Station> GetAllStations()
        {
            return new List<Station>
            {
                new Station { Id = "2000000", Name = "Москва", Region = "Москва" },
                new Station { Id = "2004000", Name = "Санкт-Петербург", Region = "Санкт-Петербург" },
                new Station { Id = "2078001", Name = "Симферополь", Region = "Крым" },
                new Station { Id = "2064188", Name = "Анапа", Region = "Краснодарский край" },
                new Station { Id = "2064130", Name = "Сочи", Region = "Краснодарский край" },
                new Station { Id = "2060615", Name = "Казань", Region = "Татарстан" },
                new Station { Id = "2064000", Name = "Ростов-на-Дону", Region = "Ростовская обл." },
                new Station { Id = "2064788", Name = "Краснодар", Region = "Краснодарский край" },
                new Station { Id = "2064110", Name = "Новороссийск", Region = "Краснодарский край" },
                new Station { Id = "2014000", Name = "Воронеж", Region = "Воронежская обл." },
                new Station { Id = "2060000", Name = "Нижний Новгород", Region = "Нижегородская обл." },
                new Station { Id = "2064150", Name = "Адлер", Region = "Краснодарский край" },
                new Station { Id = "2060001", Name = "Нижний Новгород Моск.", Region = "Нижегородская обл." },
                new Station { Id = "2010000", Name = "Ярославль", Region = "Ярославская обл." },
                new Station { Id = "2058000", Name = "Калининград", Region = "Калининградская обл." },
                new Station { Id = "2060340", Name = "Владимир", Region = "Владимирская обл." },
                new Station { Id = "2024460", Name = "Уфа", Region = "Республика Башкортостан" },
                new Station { Id = "2030000", Name = "Красноярск", Region = "Красноярский край" },
                new Station { Id = "2024000", Name = "Самара", Region = "Самарская обл." },
                new Station { Id = "2044000", Name = "Екатеринбург", Region = "Свердловская обл." },
                new Station { Id = "2038000", Name = "Новосибирск", Region = "Новосибирская обл." },
                new Station { Id = "2044730", Name = "Челябинск", Region = "Челябинская обл." },
                new Station { Id = "2020000", Name = "Волгоград", Region = "Волгоградская обл." },
                new Station { Id = "2060700", Name = "Пермь", Region = "Пермский край" },
                new Station { Id = "2060500", Name = "Ижевск", Region = "Удмуртия" },
                new Station { Id = "2060200", Name = "Оренбург", Region = "Оренбургская обл." },
                new Station { Id = "2014500", Name = "Белгород", Region = "Белгородская обл." },
                new Station { Id = "2014170", Name = "Курск", Region = "Курская обл." },
                new Station { Id = "2014400", Name = "Липецк", Region = "Липецкая обл." },
                new Station { Id = "2014300", Name = "Тамбов", Region = "Тамбовская обл." },
                new Station { Id = "2014100", Name = "Орёл", Region = "Орловская обл." },
                new Station { Id = "2013000", Name = "Брянск", Region = "Брянская обл." },
                new Station { Id = "2012000", Name = "Смоленск", Region = "Смоленская обл." },
                new Station { Id = "2011000", Name = "Тверь", Region = "Тверская обл." },
                new Station { Id = "2006000", Name = "Псков", Region = "Псковская обл." },
                new Station { Id = "2005000", Name = "Великий Новгород", Region = "Новгородская обл." },
                new Station { Id = "2008000", Name = "Петрозаводск", Region = "Карелия" },
                new Station { Id = "2002000", Name = "Мурманск", Region = "Мурманская обл." },
                new Station { Id = "2015000", Name = "Саратов", Region = "Саратовская обл." },
                new Station { Id = "2022000", Name = "Астрахань", Region = "Астраханская обл." },
                new Station { Id = "2060800", Name = "Тюмень", Region = "Тюменская обл." },
                new Station { Id = "2034000", Name = "Омск", Region = "Омская обл." },
                new Station { Id = "2032000", Name = "Томск", Region = "Томская обл." },
                new Station { Id = "2036000", Name = "Барнаул", Region = "Алтайский край" },
                new Station { Id = "2054000", Name = "Иркутск", Region = "Иркутская обл." },
                new Station { Id = "2050000", Name = "Чита", Region = "Забайкальский край" },
                new Station { Id = "2052000", Name = "Улан-Удэ", Region = "Бурятия" },
                new Station { Id = "2064200", Name = "Ставрополь", Region = "Ставропольский край" },
                new Station { Id = "2064400", Name = "Волгодонск", Region = "Ростовская обл." },
                new Station { Id = "2064600", Name = "Шахты", Region = "Ростовская обл." },
                new Station { Id = "2064800", Name = "Армавир", Region = "Краснодарский край" },
                new Station { Id = "2064900", Name = "Майкоп", Region = "Адыгея" },
                new Station { Id = "2065000", Name = "Черкесск", Region = "Карачаево-Черкесия" },
                new Station { Id = "2065100", Name = "Нальчик", Region = "Кабардино-Балкария" },
                new Station { Id = "2065200", Name = "Владикавказ", Region = "Северная Осетия" },
                new Station { Id = "2065300", Name = "Грозный", Region = "Чечня" },
                new Station { Id = "2065400", Name = "Махачкала", Region = "Дагестан" },
                new Station { Id = "2040000", Name = "Тольятти", Region = "Самарская обл." },
                new Station { Id = "2044100", Name = "Нижний Тагил", Region = "Свердловская обл." },
                new Station { Id = "2044200", Name = "Каменск-Уральский", Region = "Свердловская обл." }
            };
        }
    }
}