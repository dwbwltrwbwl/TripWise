using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using TripWise.Models;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

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
                _logger.LogInformation("=== ПОИСК РЕЙСОВ ЧЕРЕЗ REAL API ===");
                _logger.LogInformation("Откуда: {DepartureCity}", request.DepartureCity);
                _logger.LogInformation("Куда: {ArrivalCity}", request.ArrivalCity);
                _logger.LogInformation("Дата вылета: {DepartureDate}", request.DepartureDate);
                _logger.LogInformation("Дата обратно: {ReturnDate}", request.ReturnDate);
                _logger.LogInformation("Пассажиры: {Passengers}", request.Passengers);

                // Проверяем токен API
                var apiToken = _configuration["TravelPayouts:Token"];
                if (string.IsNullOrEmpty(apiToken))
                {
                    _logger.LogWarning("TravelPayouts token не настроен!");
                }
                else
                {
                    _logger.LogInformation("API токен настроен (длина: {Length})", apiToken.Length);
                }

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

                // Используем реальный сервис
                _logger.LogInformation("Начинаем поиск рейсов через AviasalesRealService...");
                var flights = await _aviasalesService.SearchFlightsAsync(request);

                _logger.LogInformation("Поиск завершен. Найдено рейсов: {Count}", flights.Count);
                _logger.LogInformation("Рейсы туда: {Count}", flights.Count(f => !f.IsReturn));
                _logger.LogInformation("Рейсы обратно: {Count}", flights.Count(f => f.IsReturn));

                return Ok(new FlightSearchResponse
                {
                    Success = true,
                    Flights = flights,
                    Message = flights.Count > 0 ? $"Найдено {flights.Count} рейсов" : "Рейсы не найдены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при поиске авиабилетов");
                _logger.LogError("StackTrace: {StackTrace}", ex.StackTrace);

                // Возвращаем более информативную ошибку
                return StatusCode(500, new FlightSearchResponse
                {
                    Success = false,
                    Error = $"Внутренняя ошибка сервера: {ex.Message}",
                    Message = "Пожалуйста, попробуйте позже"
                });
            }
        }

        [HttpGet("test")]
        public async Task<ActionResult> TestService()
        {
            try
            {
                _logger.LogInformation("=== ТЕСТ СЕРВИСА АВИАБИЛЕТОВ ===");

                // Проверяем конфигурацию
                var apiToken = _configuration["TravelPayouts:Token"];
                _logger.LogInformation("API Token настроен: {IsConfigured}", !string.IsNullOrEmpty(apiToken));

                // Создаем тестовый запрос
                var testRequest = new FlightSearchRequest
                {
                    DepartureCity = "Москва",
                    ArrivalCity = "Санкт-Петербург",
                    DepartureDate = DateTime.Now.AddDays(7),
                    ReturnDate = DateTime.Now.AddDays(14),
                    Passengers = 1,
                    Class = "economy",
                    TripType = "round"
                };

                _logger.LogInformation("Тестируем сервис с запросом: {Request}",
                    JsonSerializer.Serialize(testRequest));

                var flights = await _aviasalesService.SearchFlightsAsync(testRequest);

                return Ok(new
                {
                    success = true,
                    message = "Сервис работает",
                    flightsCount = flights.Count,
                    apiTokenConfigured = !string.IsNullOrEmpty(apiToken),
                    apiTokenLength = apiToken?.Length ?? 0
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

        [HttpGet("check-config")]
        public ActionResult CheckConfig()
        {
            try
            {
                _logger.LogInformation("=== ПРОВЕРКА КОНФИГУРАЦИИ ===");

                var apiToken = _configuration["TravelPayouts:Token"];
                var hasToken = !string.IsNullOrEmpty(apiToken);

                _logger.LogInformation("TravelPayouts Token: {HasToken} (длина: {Length})",
                    hasToken, apiToken?.Length ?? 0);

                return Ok(new
                {
                    hasToken,
                    tokenLength = apiToken?.Length ?? 0,
                    tokenPreview = hasToken ? apiToken.Substring(0, Math.Min(10, apiToken.Length)) + "..." : "не настроен",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке конфигурации");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("debug")]
        public ActionResult DebugInfo()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var location = assembly.Location;
            var version = assembly.GetName().Version.ToString();

            return Ok(new
            {
                timestamp = DateTime.Now,
                assemblyLocation = location,
                assemblyVersion = version,
                machineName = Environment.MachineName,
                osVersion = Environment.OSVersion.ToString(),
                is64Bit = Environment.Is64BitProcess,
                configKeys = _configuration.AsEnumerable().Where(x => x.Key.Contains("TravelPayouts", StringComparison.OrdinalIgnoreCase)).ToList()
            });
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

                _logger.LogInformation("Поиск городов по запросу: '{Query}'", query);

                var cities = await _aviasalesService.SearchCitiesAsync(query);
                var result = cities.Take(15).ToList();

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