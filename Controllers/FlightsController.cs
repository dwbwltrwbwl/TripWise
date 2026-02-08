using Microsoft.AspNetCore.Mvc;
using TripWise.Models;
using TripWise.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly IFlightService _flightService;
        private readonly IFlightOrderService _flightOrderService;
        private readonly ILogger<FlightsController> _logger;
        private readonly TripWiseContext _context;

        public FlightsController(IFlightService flightService, IFlightOrderService flightOrderService, ILogger<FlightsController> logger, TripWiseContext context)
        {
            _flightService = flightService;
            _flightOrderService = flightOrderService;
            _logger = logger;
            _context = context;
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

        [HttpPost("book")]
        public async Task<ActionResult<FlightOrderResponse>> BookFlight([FromBody] FlightOrderRequest request)
        {
            try
            {
                _logger.LogInformation("=== БРОНИРОВАНИЕ РЕЙСА ===");
                _logger.LogInformation("Запрос: {@Request}", request);

                // Проверяем авторизацию (но не требуем её)
                int? userId = null;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
                {
                    userId = parsedUserId;
                    _logger.LogInformation("Пользователь авторизован, ID: {UserId}", userId);
                }
                else
                {
                    _logger.LogInformation("Пользователь не авторизован, создаем заказ без привязки к аккаунту");
                }

                // Валидация
                if (request == null)
                    return BadRequest(new FlightOrderResponse { Success = false, Message = "Запрос не может быть пустым" });

                if (request.Passengers == null || !request.Passengers.Any())
                    return BadRequest(new FlightOrderResponse { Success = false, Message = "Добавьте хотя бы одного пассажира" });

                if (request.Contact == null)
                    return BadRequest(new FlightOrderResponse { Success = false, Message = "Укажите контактные данные" });

                if (request.Payment == null)
                    return BadRequest(new FlightOrderResponse { Success = false, Message = "Укажите данные для оплаты" });

                // Если пользователь не авторизован, создаем временный ID
                int tempUserId = userId ?? -1; // Используем -1 для неавторизованных пользователей

                // Создаем заказ с демо-оплатой
                var result = await _flightOrderService.ProcessDemoPaymentAsync(request, tempUserId);

                _logger.LogInformation("Заказ создан: {@Result}", result);

                // Имитируем отправку билета на email
                await SendDemoEmailConfirmation(result, request.Contact.Email);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при бронировании рейса");
                return StatusCode(500, new FlightOrderResponse
                {
                    Success = false,
                    Message = "Ошибка при бронировании рейса",
                    OrderId = null
                });
            }
        }

        [Authorize]
        [HttpGet("my-orders")]
        public async Task<ActionResult> GetMyOrders()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new { Success = false, Message = "Требуется авторизация" });
                }

                var orders = await _flightOrderService.GetUserOrdersAsync(userId);

                return Ok(new
                {
                    Success = true,
                    Orders = orders,
                    Count = orders.Count,
                    Message = "Ваши заказы"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении заказов");
                return StatusCode(500, new { Success = false, Message = "Ошибка при получении заказов" });
            }
        }

        [Authorize]
        [HttpGet("order/{orderId}")]
        public async Task<ActionResult> GetOrder(string orderId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new { Success = false, Message = "Требуется авторизация" });
                }

                var order = await _flightOrderService.GetOrderByIdAsync(orderId);

                if (order == null)
                    return NotFound(new { Success = false, Message = "Заказ не найден" });

                if (order.UserId != userId)
                    return Forbid();

                return Ok(new
                {
                    Success = true,
                    Order = order,
                    Message = "Информация о заказе"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении заказа");
                return StatusCode(500, new { Success = false, Message = "Ошибка при получении заказа" });
            }
        }

        [Authorize]
        [HttpPost("order/{orderId}/cancel")]
        public async Task<ActionResult> CancelOrder(string orderId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new { Success = false, Message = "Требуется авторизация" });
                }

                var success = await _flightOrderService.CancelOrderAsync(orderId, userId);

                if (!success)
                    return BadRequest(new { Success = false, Message = "Не удалось отменить заказ" });

                return Ok(new
                {
                    Success = true,
                    Message = "Заказ успешно отменен",
                    RefundNote = "Средства будут возвращены в течение 3-5 рабочих дней"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене заказа");
                return StatusCode(500, new { Success = false, Message = "Ошибка при отмене заказа" });
            }
        }

        [HttpGet("ticket/{ticketNumber}")]
        public async Task<ActionResult> GetTicketInfo(string ticketNumber)
        {
            try
            {
                var order = await _context.FlightOrders
                    .Include(o => o.Passengers)
                    .FirstOrDefaultAsync(o => o.TicketNumber == ticketNumber);

                if (order == null)
                    return NotFound(new { Success = false, Message = "Билет не найден" });

                // Маскируем конфиденциальные данные для публичного просмотра
                var maskedOrder = new
                {
                    order.Airline,
                    order.FlightNumber,
                    order.DepartureCity,
                    order.ArrivalCity,
                    order.DepartureAirport,
                    order.ArrivalAirport,
                    order.DepartureTime,
                    order.ArrivalTime,
                    order.Status,
                    Passengers = order.Passengers.Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        p.SeatNumber,
                        p.Baggage
                    }),
                    BoardingTime = order.DepartureTime.AddHours(-2),
                    Gate = "Демо-выход A1",
                    Terminal = "T1"
                };

                return Ok(new
                {
                    Success = true,
                    Ticket = maskedOrder,
                    Message = "Информация о билете"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о билете");
                return StatusCode(500, new { Success = false, Message = "Ошибка при получении информации о билете" });
            }
        }

        private async Task SendDemoEmailConfirmation(FlightOrderResponse order, string email)
        {
            // Имитация отправки email
            _logger.LogInformation($"Демо-email отправлен на {email}");
            _logger.LogInformation($"Тема: Подтверждение бронирования рейса #{order.OrderNumber}");
            _logger.LogInformation($"Тело письма содержит информацию о заказе и билете");

            // В реальном приложении здесь была бы интеграция с Email сервисом
            await Task.Delay(100); // Имитация отправки
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