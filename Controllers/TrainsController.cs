using Microsoft.AspNetCore.Mvc;
using TripWise.Models;
using TripWise.Models.ViewModels;
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
                _logger.LogInformation("=== ПОИСК ПОЕЗДОВ ===");
                _logger.LogInformation("Запрос: {@Request}", request);

                var allTrainGroups = new List<TrainGroupResponse>();

                // 1. Ищем поезда туда
                var departureRequest = new TrainSearchRequest
                {
                    DepartureStationId = request.DepartureStationId,
                    ArrivalStationId = request.ArrivalStationId,
                    DepartureDate = request.DepartureDate,
                    Passengers = request.Passengers,
                    IsReturn = false,
                    ReturnDate = null
                };

                _logger.LogInformation("Поиск поездов ТУДА...");
                var forwardTrains = await _rzdApiService.SearchTrains(departureRequest);
                _logger.LogInformation("Найдено поездов ТУДА: {Count}", forwardTrains.Count);

                // 2. Ищем поезда обратно (если указана обратная дата)
                List<TrainSearchResponse> returnTrains = new List<TrainSearchResponse>();
                if (!string.IsNullOrEmpty(request.ReturnDate))
                {
                    _logger.LogInformation("Поиск поездов ОБРАТНО...");
                    var returnRequest = new TrainSearchRequest
                    {
                        DepartureStationId = request.ArrivalStationId,
                        ArrivalStationId = request.DepartureStationId,
                        DepartureDate = request.ReturnDate,
                        Passengers = request.Passengers,
                        IsReturn = true,
                        ReturnDate = null
                    };

                    returnTrains = await _rzdApiService.SearchTrains(returnRequest);
                    _logger.LogInformation("Найдено поездов ОБРАТНО: {Count}", returnTrains.Count);
                }

                // 3. Группируем рейсы в комбинированные карточки
                if (!string.IsNullOrEmpty(request.ReturnDate) && forwardTrains.Count > 0 && returnTrains.Count > 0)
                {
                    // Создаем комбинированные карточки (туда + обратно)
                    _logger.LogInformation("Создание комбинированных карточек...");

                    // Берем первые 5 поездов в каждую сторону для комбинаций
                    var forwardForCombination = forwardTrains.Take(5).ToList();
                    var returnForCombination = returnTrains.Take(5).ToList();

                    foreach (var forwardTrain in forwardForCombination)
                    {
                        foreach (var returnTrain in returnForCombination)
                        {
                            // Рассчитываем общую цену (минимальная цена туда + минимальная цена обратно)
                            var forwardMinPrice = forwardTrain.Categories?.Min(c => c.Price) ?? 0;
                            var returnMinPrice = returnTrain.Categories?.Min(c => c.Price) ?? 0;
                            var totalPrice = forwardMinPrice + returnMinPrice;

                            allTrainGroups.Add(new TrainGroupResponse
                            {
                                Id = $"{forwardTrain.TrainNumber}-{returnTrain.TrainNumber}",
                                ForwardTrain = forwardTrain,
                                ReturnTrain = returnTrain,
                                TotalPrice = totalPrice,
                                IsRoundTrip = true
                            });
                        }
                    }

                    _logger.LogInformation("Создано комбинированных карточек: {Count}", allTrainGroups.Count);
                }
                else
                {
                    // Если нет обратной даты или нет поездов - показываем только поезда туда
                    _logger.LogInformation("Создание отдельных карточек...");
                    foreach (var train in forwardTrains)
                    {
                        allTrainGroups.Add(new TrainGroupResponse
                        {
                            Id = train.TrainNumber,
                            ForwardTrain = train,
                            ReturnTrain = null,
                            TotalPrice = train.Categories?.Min(c => c.Price) ?? 0,
                            IsRoundTrip = false
                        });
                    }
                }

                _logger.LogInformation("=== ИТОГИ ===");
                _logger.LogInformation("Всего карточек: {TotalCount}", allTrainGroups.Count);
                _logger.LogInformation("Комбинированных: {CombinedCount}", allTrainGroups.Count(g => g.IsRoundTrip));
                _logger.LogInformation("Одиночных: {SingleCount}", allTrainGroups.Count(g => !g.IsRoundTrip));

                return Ok(new
                {
                    success = true,
                    trainGroups = allTrainGroups,
                    message = allTrainGroups.Count > 0 ? $"Найдено {allTrainGroups.Count} вариантов" : "Варианты не найдены"
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

            var allStations = GetAllStationsData();
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
            var stations = GetAllStationsData()
                .Select(s => new { id = s.Id, name = s.Name, region = s.Region })
                .ToList();

            return Ok(stations);
        }
        // GET: /Railway/Book
        [HttpGet]
        public IActionResult Book(string trainNumber, string departureStationId, string departureStationName,
                                 string arrivalStationId, string arrivalStationName, DateTime departureDateTime,
                                 DateTime? arrivalDateTime, decimal price, int passengers, string carType,
                                 string carClass, int duration, bool isRoundTrip, string? returnTrainNumber = null,
                                 DateTime? returnDepartureDateTime = null, DateTime? returnArrivalDateTime = null,
                                 int? returnDuration = null)
        {
            var model = new TrainBookingViewModel
            {
                TrainNumber = trainNumber,
                ReturnTrainNumber = returnTrainNumber,
                DepartureStationId = departureStationId,
                DepartureStationName = departureStationName,
                ArrivalStationId = arrivalStationId,
                ArrivalStationName = arrivalStationName,
                DepartureDateTime = departureDateTime,
                ArrivalDateTime = arrivalDateTime,
                ReturnDepartureDateTime = returnDepartureDateTime,
                ReturnArrivalDateTime = returnArrivalDateTime,
                Price = price,
                Passengers = passengers,
                CarType = carType,
                CarClass = carClass,
                Duration = duration,
                ReturnDuration = returnDuration,
                IsRoundTrip = isRoundTrip
            };

            // Перенаправляем на Book в Home контроллере
            return RedirectToAction("Book", "Home", model);
        }
        private List<Station> GetAllStationsData()
        {
            return new List<Station>
            {
                new Station { Id = "2000000", Name = "Москва", Region = "Москва" },
                new Station { Id = "2004000", Name = "Санкт-Петербург", Region = "Санкт-Петербург" },
                new Station { Id = "2060000", Name = "Нижний Новгород", Region = "Нижегородская обл." },
                new Station { Id = "2064000", Name = "Ростов-на-Дону", Region = "Ростовская обл." },
                new Station { Id = "2024000", Name = "Самара", Region = "Самарская обл." },
                new Station { Id = "2024460", Name = "Уфа", Region = "Республика Башкортостан" },
                new Station { Id = "2030000", Name = "Красноярск", Region = "Красноярский край" },
                new Station { Id = "2014000", Name = "Воронеж", Region = "Воронежская обл." },
                new Station { Id = "2044000", Name = "Екатеринбург", Region = "Свердловская обл." },
                new Station { Id = "2038000", Name = "Новосибирск", Region = "Новосибирская обл." },
                new Station { Id = "2060501", Name = "Казань", Region = "Татарстан" },
                new Station { Id = "2064130", Name = "Сочи", Region = "Краснодарский край" },
                new Station { Id = "2064110", Name = "Новороссийск", Region = "Краснодарский край" },
                new Station { Id = "2064788", Name = "Краснодар", Region = "Краснодарский край" },
                new Station { Id = "2064188", Name = "Анапа", Region = "Краснодарский край" },
                new Station { Id = "2078001", Name = "Симферополь", Region = "Крым" },
                new Station { Id = "2064150", Name = "Адлер", Region = "Краснодарский край" }
            };
        }
    }
}