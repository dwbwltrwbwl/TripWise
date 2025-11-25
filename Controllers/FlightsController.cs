using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using TripWise.Models;
using System.Text.Json;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly IAviasalesRealService _aviasalesService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FlightsController> _logger;

        public FlightsController(IAviasalesRealService aviasalesService,
                               IConfiguration configuration,
                               ILogger<FlightsController> logger)
        {
            _aviasalesService = aviasalesService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("search")]
        public async Task<ActionResult<FlightSearchResponse>> SearchFlights([FromBody] FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Получен запрос на поиск рейсов: {@Request}", request);

                // Валидация
                var validationError = ValidateFlightSearchRequest(request);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return BadRequest(new FlightSearchResponse
                    {
                        Success = false,
                        Error = validationError
                    });
                }

                var flights = await _aviasalesService.SearchFlightsAsync(request);

                _logger.LogInformation("Найдено рейсов: {Count}", flights.Count);

                return Ok(new FlightSearchResponse
                {
                    Success = true,
                    Flights = flights,
                    Message = flights.Count > 0 ? $"Найдено {flights.Count} рейсов" : "Рейсы не найдены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске авиабилетов");
                return StatusCode(500, new FlightSearchResponse
                {
                    Success = false,
                    Error = "Внутренняя ошибка сервера",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("cities")]
        public async Task<ActionResult<CitySearchResponse>> SearchCities([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new CitySearchResponse
                    {
                        Success = true,
                        Cities = new List<City>(),
                        Message = "Введите минимум 2 символа для поиска"
                    });
                }

                _logger.LogInformation("Поиск городов по запросу: {Query}", query);

                var cities = await _aviasalesService.SearchCitiesAsync(query);
                var result = cities.Take(15).ToList(); // Ограничиваем количество результатов

                _logger.LogInformation("Найдено городов: {Count}", result.Count);

                return Ok(new CitySearchResponse
                {
                    Success = true,
                    Cities = result,
                    Message = result.Count > 0 ? $"Найдено {result.Count} городов" : "Городы не найдены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов для запроса: {Query}", query);
                return Ok(new CitySearchResponse
                {
                    Success = false,
                    Cities = new List<City>(),
                    Error = "Ошибка при поиске городов"
                });
            }
        }

        [HttpGet("test-connection")]
        public async Task<ActionResult> TestConnection()
        {
            try
            {
                _logger.LogInformation("Тестирование подключения к API");

                // Простой тест - поиск популярных городов
                var cities = await _aviasalesService.SearchCitiesAsync("Москва");

                return Ok(new
                {
                    success = true,
                    message = "Подключение к API работает нормально",
                    citiesCount = cities.Count,
                    sampleCities = cities.Take(3).Select(c => new { c.Name, c.Code, c.Type })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестировании подключения к API");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Ошибка подключения к API",
                    details = ex.Message
                });
            }
        }

        [HttpGet("debug-test")]
        public async Task<ActionResult> DebugTest()
        {
            try
            {
                _logger.LogInformation("=== ТЕСТИРОВАНИЕ СЕРВИСА ===");

                // Тест поиска городов
                _logger.LogInformation("Тест поиска городов...");
                var cities = await _aviasalesService.SearchCitiesAsync("Москва");
                _logger.LogInformation("Найдено городов: {Count}", cities.Count);

                // Тест поиска рейсов
                _logger.LogInformation("Тест поиска рейсов...");
                var testRequest = new FlightSearchRequest
                {
                    DepartureCity = "Москва",
                    ArrivalCity = "Санкт-Петербург",
                    DepartureDate = DateTime.Now.AddDays(7),
                    Passengers = 1
                };
                var flights = await _aviasalesService.SearchFlightsAsync(testRequest);
                _logger.LogInformation("Найдено рейсов: {Count}", flights.Count);

                return Ok(new
                {
                    success = true,
                    citiesCount = cities.Count,
                    flightsCount = flights.Count,
                    message = "Сервис работает корректно"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестировании сервиса");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("simple-test")]
        public ActionResult SimpleTest()
        {
            return Ok(new
            {
                message = "Контроллер работает!",
                timestamp = DateTime.Now,
                token = _configuration["TravelPayouts:Token"]?.Substring(0, 10) + "..."
            });
        }

        // Вспомогательные методы
        private string ValidateFlightSearchRequest(FlightSearchRequest request)
        {
            if (request == null)
                return "Запрос не может быть пустым";

            if (string.IsNullOrEmpty(request.DepartureCity))
                return "Город вылета обязателен";

            if (string.IsNullOrEmpty(request.ArrivalCity))
                return "Город прилета обязателен";

            if (request.DepartureDate < DateTime.Today)
                return "Дата вылета не может быть в прошлом";

            if (request.ReturnDate.HasValue && request.ReturnDate < request.DepartureDate)
                return "Дата возвращения не может быть раньше даты вылета";

            if (request.Passengers < 1 || request.Passengers > 9)
                return "Количество пассажиров должно быть от 1 до 9";

            if (request.DepartureDate > DateTime.Today.AddYears(1))
                return "Дата вылета не может быть более чем на год вперед";

            return null;
        }
    }
}