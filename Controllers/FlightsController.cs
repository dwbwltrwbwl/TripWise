using Microsoft.AspNetCore.Mvc;
using TripWise.Models;
using TripWise.Services;
using Microsoft.Extensions.Logging;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly IFlightService _flightService;
        private readonly ILogger<FlightsController> _logger;

        public FlightsController(IFlightService flightService, ILogger<FlightsController> logger)
        {
            _flightService = flightService;
            _logger = logger;
        }

        [HttpPost("search")]
        public async Task<ActionResult<FlightSearchResponse>> SearchFlights([FromBody] FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("=== ПОИСК РЕЙСОВ API ===");
                _logger.LogInformation("Запрос получен: {@Request}", request);

                // Валидация запроса
                var validationError = ValidateFlightSearchRequest(request);
                if (!string.IsNullOrEmpty(validationError))
                {
                    _logger.LogWarning("Ошибка валидации: {Error}", validationError);
                    return BadRequest(new FlightSearchResponse
                    {
                        Success = false,
                        Error = validationError
                    });
                }

                _logger.LogInformation("Параметры поиска:");
                _logger.LogInformation("- Откуда: {DepartureCity}", request.DepartureCity);
                _logger.LogInformation("- Куда: {ArrivalCity}", request.ArrivalCity);
                _logger.LogInformation("- Дата вылета: {DepartureDate}", request.DepartureDate);
                _logger.LogInformation("- Дата обратно: {ReturnDate}", request.ReturnDate);
                _logger.LogInformation("- Пассажиры: {Passengers}", request.Passengers);
                _logger.LogInformation("- Класс: {Class}", request.Class);
                _logger.LogInformation("- Тип: {TripType}", request.TripType);

                // Выполняем поиск рейсов
                var result = await _flightService.SearchFlightsAsync(request);

                _logger.LogInformation("Результат поиска:");
                _logger.LogInformation("- Успех: {Success}", result.Success);
                _logger.LogInformation("- Найдено рейсов: {Count}", result.Flights?.Count ?? 0);
                _logger.LogInformation("- ID поиска: {SearchId}", result.SearchId);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    _logger.LogError("Ошибка поиска: {Error}", result.Error);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске рейсов");
                return StatusCode(500, new FlightSearchResponse
                {
                    Success = false,
                    Error = "Внутренняя ошибка сервера",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("cities")]
        public async Task<ActionResult> SearchCities([FromQuery] string query)
        {
            try
            {
                _logger.LogInformation("Поиск городов: {Query}", query);

                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new
                    {
                        Success = true,
                        Cities = new List<City>(),
                        Message = "Введите минимум 2 символа"
                    });
                }

                var cities = await _flightService.SearchCitiesAsync(query);

                _logger.LogInformation("Найдено городов: {Count}", cities.Count);

                return Ok(new
                {
                    Success = true,
                    Cities = cities,
                    Message = $"Найдено {cities.Count} городов"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = "Ошибка при поиске городов",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("popular-cities")]
        public async Task<ActionResult> GetPopularCities()
        {
            try
            {
                _logger.LogInformation("Запрос популярных городов");

                var cities = await _flightService.GetPopularCitiesAsync();

                _logger.LogInformation("Отправлено популярных городов: {Count}", cities.Count);

                return Ok(new
                {
                    Success = true,
                    Cities = cities,
                    Message = "Популярные города для путешествий"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении популярных городов");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = "Ошибка при получении городов",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("test")]
        public async Task<ActionResult> TestService()
        {
            try
            {
                _logger.LogInformation("Тестирование сервиса авиабилетов");

                var testRequest = new FlightSearchRequest
                {
                    DepartureCity = "Москва (MOW)",
                    ArrivalCity = "Санкт-Петербург (LED)",
                    DepartureDate = DateTime.Now.AddDays(7),
                    ReturnDate = DateTime.Now.AddDays(14),
                    Passengers = 2,
                    Class = "economy",
                    TripType = "round"
                };

                var result = await _flightService.SearchFlightsAsync(testRequest);

                return Ok(new
                {
                    Success = true,
                    Message = "Сервис авиабилетов работает корректно",
                    FlightsCount = result.Flights?.Count ?? 0,
                    SearchId = result.SearchId,
                    TestRequest = testRequest
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестировании сервиса");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    Message = "Сервис авиабилетов временно недоступен"
                });
            }
        }

        [HttpGet("test-search")]
        public async Task<ActionResult> TestSearch()
        {
            try
            {
                _logger.LogInformation("Тестовый поиск рейсов");

                var testRequest = new FlightSearchRequest
                {
                    DepartureCity = "Москва (MOW)",
                    ArrivalCity = "Санкт-Петербург (LED)",
                    DepartureDate = DateTime.Now.AddDays(7),
                    ReturnDate = DateTime.Now.AddDays(14),
                    Passengers = 2,
                    Class = "economy",
                    TripType = "round"
                };

                _logger.LogInformation("Тестовый запрос: {@Request}", testRequest);

                var result = await _flightService.SearchFlightsAsync(testRequest);

                return Ok(new
                {
                    TestRequest = testRequest,
                    SearchResult = result,
                    ServerTime = DateTime.Now,
                    Status = "OK"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка тестового поиска");
                return StatusCode(500, new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    ServerTime = DateTime.Now,
                    Status = "ERROR"
                });
            }
        }

        [HttpGet("debug")]
        public ActionResult Debug()
        {
            var endpointInfo = new
            {
                Timestamp = DateTime.Now,
                Endpoint = "/api/flights/search",
                Method = "POST",
                RequiredHeaders = new
                {
                    ContentType = "application/json"
                },
                ExpectedModel = new
                {
                    DepartureCity = "string (например: 'Москва' или 'Москва (MOW)')",
                    ArrivalCity = "string (например: 'Санкт-Петербург' или 'Санкт-Петербург (LED)')",
                    DepartureDate = "string (формат: YYYY-MM-DD)",
                    ReturnDate = "string (формат: YYYY-MM-DD) или null",
                    Passengers = "integer (от 1 до 9)",
                    Class = "string (economy, business, first)",
                    TripType = "string (oneway или round)"
                },
                ExampleRequest = new
                {
                    DepartureCity = "Москва",
                    ArrivalCity = "Санкт-Петербург",
                    DepartureDate = "2024-12-20",
                    ReturnDate = "2024-12-27",
                    Passengers = 2,
                    Class = "economy",
                    TripType = "round"
                }
            };

            return Ok(new
            {
                Success = true,
                Message = "Информация о API авиабилетов",
                ServerInfo = new
                {
                    ServerTime = DateTime.Now,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                },
                Endpoints = new[]
                {
                    new { Path = "/api/flights/search", Method = "POST", Description = "Поиск рейсов" },
                    new { Path = "/api/flights/cities", Method = "GET", Description = "Поиск городов" },
                    new { Path = "/api/flights/popular-cities", Method = "GET", Description = "Популярные города" },
                    new { Path = "/api/flights/test", Method = "GET", Description = "Тест сервиса" },
                    new { Path = "/api/flights/test-search", Method = "GET", Description = "Тестовый поиск" },
                    new { Path = "/api/flights/debug", Method = "GET", Description = "Отладочная информация" }
                },
                Details = endpointInfo
            });
        }

        [HttpGet("health")]
        public ActionResult HealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.Now,
                Service = "Flights API",
                Version = "1.0.0"
            });
        }

        [HttpGet("route-info/{fromCity}/{toCity}")]
        public async Task<ActionResult> GetRouteInfo(string fromCity, string toCity)
        {
            try
            {
                _logger.LogInformation("Получение информации о маршруте: {From} -> {To}", fromCity, toCity);

                if (string.IsNullOrEmpty(fromCity) || string.IsNullOrEmpty(toCity))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Error = "Необходимо указать города отправления и назначения"
                    });
                }

                var routeInfo = await _flightService.GetRouteInfoAsync(fromCity, toCity);

                return Ok(new
                {
                    Success = true,
                    RouteInfo = routeInfo,
                    Message = $"Информация о маршруте {fromCity} → {toCity}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о маршруте");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = "Ошибка при получении информации о маршруте",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("sample-response")]
        public ActionResult GetSampleResponse()
        {
            var sampleResponse = new FlightSearchResponse
            {
                Success = true,
                Message = "Демонстрационные данные",
                SearchId = Guid.NewGuid().ToString(),
                Flights = new List<Flight>
                {
                    new Flight
                    {
                        Id = "SU-1234",
                        Airline = "Аэрофлот",
                        AirlineCode = "SU",
                        FlightNumber = "SU 1234",
                        DepartureCity = "Москва",
                        ArrivalCity = "Санкт-Петербург",
                        DepartureAirport = "SVO",
                        ArrivalAirport = "LED",
                        DepartureTime = DateTime.Now.AddDays(1).AddHours(8),
                        ArrivalTime = DateTime.Now.AddDays(1).AddHours(10),
                        Price = 4500,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 120,
                        Aircraft = "Airbus A320",
                        IsReturn = false,
                        BookingUrl = "https://www.aviasales.ru/search",
                        Details = new FlightDetails
                        {
                            IsRefundable = true,
                            IsChangeable = true,
                            Baggage = "1x23кг",
                            HandLuggage = "1x10кг",
                            Meal = "Завтрак"
                        }
                    },
                    new Flight
                    {
                        Id = "S7-5678",
                        Airline = "S7 Airlines",
                        AirlineCode = "S7",
                        FlightNumber = "S7 5678",
                        DepartureCity = "Москва",
                        ArrivalCity = "Санкт-Петербург",
                        DepartureAirport = "DME",
                        ArrivalAirport = "LED",
                        DepartureTime = DateTime.Now.AddDays(1).AddHours(14),
                        ArrivalTime = DateTime.Now.AddDays(1).AddHours(16),
                        Price = 5200,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 120,
                        Aircraft = "Boeing 737",
                        IsReturn = false,
                        BookingUrl = "https://www.aviasales.ru/search",
                        Details = new FlightDetails
                        {
                            IsRefundable = false,
                            IsChangeable = true,
                            Baggage = "1x23кг",
                            HandLuggage = "1x10кг",
                            Meal = "Обед"
                        }
                    }
                },
                PartnerLinks = new PartnerLinks
                {
                    AviasalesUrl = "https://www.aviasales.ru/search",
                    YandexTravelUrl = "https://travel.yandex.ru/avia",
                    TutuUrl = "https://www.tutu.ru/avia",
                    SkyscannerUrl = "https://www.skyscanner.ru"
                }
            };

            return Ok(sampleResponse);
        }

        private string ValidateFlightSearchRequest(FlightSearchRequest request)
        {
            if (request == null)
                return "Запрос не может быть пустым";

            if (string.IsNullOrWhiteSpace(request.DepartureCity))
                return "Город вылета обязателен";

            if (string.IsNullOrWhiteSpace(request.ArrivalCity))
                return "Город прилета обязателен";

            if (request.DepartureDate == default)
                return "Дата вылета обязательна";

            if (request.DepartureDate < DateTime.Today)
                return "Дата вылета не может быть в прошлом";

            if (request.ReturnDate.HasValue && request.ReturnDate.Value < request.DepartureDate)
                return "Дата обратного вылета не может быть раньше даты вылета";

            if (request.Passengers < 1 || request.Passengers > 9)
                return "Количество пассажиров должно быть от 1 до 9";

            if (!string.IsNullOrEmpty(request.Class) &&
                !new[] { "economy", "business", "first" }.Contains(request.Class.ToLower()))
                return "Класс должен быть: economy, business или first";

            if (!string.IsNullOrEmpty(request.TripType) &&
                !new[] { "oneway", "round" }.Contains(request.TripType.ToLower()))
                return "Тип поездки должен быть: oneway или round";

            return null;
        }
    }
}