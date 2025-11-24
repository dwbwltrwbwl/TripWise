using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using TripWise.Models;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HotelsController : ControllerBase
    {
        private readonly IHotelService _hotelService;
        private readonly ILogger<HotelsController> _logger;

        public HotelsController(IHotelService hotelService, ILogger<HotelsController> logger)
        {
            _hotelService = hotelService;
            _logger = logger;
        }

        [HttpPost("search")]
        public async Task<ActionResult<HotelSearchResponse>> SearchHotels([FromBody] HotelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Получен запрос на поиск отелей: {@Request}", request);

                // Валидация
                var validationError = ValidateHotelSearchRequest(request);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return BadRequest(new HotelSearchResponse
                    {
                        Success = false,
                        Error = validationError
                    });
                }

                var hotels = await _hotelService.SearchHotelsAsync(request);

                _logger.LogInformation("Найдено отелей: {Count}", hotels.Count);

                return Ok(new HotelSearchResponse
                {
                    Success = true,
                    Hotels = hotels,
                    Message = hotels.Count > 0 ? $"Найдено {hotels.Count} отелей" : "Отели не найдены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске отелей");
                return StatusCode(500, new HotelSearchResponse
                {
                    Success = false,
                    Error = "Ошибка при поиске отелей",
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

                _logger.LogInformation("Поиск городов для отелей: {Query}", query);

                var cities = await _hotelService.SearchHotelCitiesAsync(query);
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
                _logger.LogError(ex, "Ошибка при поиске городов для отелей");
                return Ok(new CitySearchResponse
                {
                    Success = false,
                    Cities = new List<City>(),
                    Error = "Ошибка при поиске городов"
                });
            }
        }

        [HttpGet("test")]
        public async Task<ActionResult> TestConnection()
        {
            try
            {
                _logger.LogInformation("Тестирование подключения к Hotel API");

                var cities = await _hotelService.SearchHotelCitiesAsync("Москва");

                return Ok(new
                {
                    success = true,
                    message = "Hotel API работает нормально",
                    citiesCount = cities.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестировании Hotel API");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Ошибка подключения к Hotel API",
                    details = ex.Message
                });
            }
        }

        private string ValidateHotelSearchRequest(HotelSearchRequest request)
        {
            if (request == null)
                return "Запрос не может быть пустым";

            if (string.IsNullOrEmpty(request.City))
                return "Город обязателен";

            if (request.CheckIn < DateTime.Today)
                return "Дата заезда не может быть в прошлом";

            if (request.CheckOut <= request.CheckIn)
                return "Дата выезда должна быть после даты заезда";

            if (request.Adults < 1 || request.Adults > 10)
                return "Количество взрослых должно быть от 1 до 10";

            if (request.Rooms < 1 || request.Rooms > 5)
                return "Количество комнат должно быть от 1 до 5";

            return null;
        }
    }
}
