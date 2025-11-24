using System.Text.Json;
using TripWise.Models;

namespace TripWise.Services
{
    public interface IHotelService
    {
        Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request);
        Task<List<City>> SearchHotelCitiesAsync(string query);
    }

    public class HotelService : IHotelService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HotelService> _logger;

        public HotelService(HttpClient httpClient, IConfiguration configuration, ILogger<HotelService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Поиск отелей: {@Request}", request);

                var locationId = await GetLocationId(request.City);
                if (locationId == 0)
                {
                    _logger.LogWarning("Не удалось найти ID локации для города: {City}", request.City);
                    throw new Exception($"Город '{request.City}' не найден");
                }

                var hotels = await SearchHotelsViaAPI(locationId, request);
                return hotels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске отелей");
                throw;
            }
        }

        private async Task<List<Hotel>> SearchHotelsViaAPI(int locationId, HotelSearchRequest request)
        {
            var token = _configuration["TravelPayouts:Token"];

            var url = $"https://engine.hotellook.com/api/v2/cache.json?" +
                     $"locationId={locationId}&" +
                     $"checkIn={request.CheckIn:yyyy-MM-dd}&" +
                     $"checkOut={request.CheckOut:yyyy-MM-dd}&" +
                     $"adults={request.Adults}&" +
                     $"children={request.Children}&" +
                     $"rooms={request.Rooms}&" +
                     $"currency=rub&" +
                     $"token={token}";

            _logger.LogInformation("Запрос к HotelLook API: {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ошибка HotelLook API: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new Exception($"API ошибка: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Ответ HotelLook API: {Json}", json);

            var apiResponse = JsonSerializer.Deserialize<HotelLookSearchResponse>(json);

            if (apiResponse?.Results?.Hotels == null || !apiResponse.Success)
            {
                _logger.LogWarning("Пустой или ошибочный ответ от HotelLook API");
                throw new Exception("Не удалось получить данные отелей");
            }

            return ConvertToHotels(apiResponse.Results.Hotels, request.City);
        }

        private List<Hotel> ConvertToHotels(List<HotelLookHotel> hotelData, string cityName)
        {
            var hotels = new List<Hotel>();

            foreach (var data in hotelData)
            {
                try
                {
                    var hotel = new Hotel
                    {
                        Id = data.HotelId.ToString(),
                        Name = data.HotelName ?? "Отель без названия",
                        Address = data.Address ?? "Адрес не указан",
                        Price = data.PriceAvg > 0 ? data.PriceAvg : data.Price,
                        Rating = data.Rating,
                        Stars = data.Stars,
                        Description = $"Отель {data.Stars} звезд в {cityName}",
                        Photos = new List<string>(),
                        Amenities = new List<string>(),
                        Location = new Location
                        {
                            Lat = data.Location?.Geo?.Lat ?? 0,
                            Lng = data.Location?.Geo?.Lon ?? 0,
                            City = cityName,
                            Country = data.Location?.Country ?? "Россия"
                        }
                    };

                    // Добавляем фото если есть
                    if (data.PhotosCount > 0)
                    {
                        hotel.Photos.Add($"https://photo.hotellook.com/image_v2/limit/h{data.HotelId}_1/800/520.auto");
                    }

                    hotels.Add(hotel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при конвертации отеля {HotelId}", data.HotelId);
                }
            }

            return hotels.OrderBy(h => h.Price).ToList();
        }

        public async Task<List<City>> SearchHotelCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                var url = $"https://engine.hotellook.com/api/v2/lookup.json?" +
                         $"query={Uri.EscapeDataString(query)}&" +
                         $"lang=ru&" +
                         $"lookFor=both";

                _logger.LogInformation("Поиск городов для отелей: {Query}", query);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка API: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var citiesData = JsonSerializer.Deserialize<HotelCityLookupResponse>(json);

                var cities = new List<City>();
                foreach (var location in citiesData?.Results?.Locations ?? new List<HotelLookupLocation>())
                {
                    var city = new City
                    {
                        Code = location.Id.ToString(),
                        Name = location.Name ?? "",
                        Country = location.Country ?? "",
                        CountryCode = "",
                        Type = "city",
                        Airport = ""
                    };

                    if (!string.IsNullOrEmpty(city.Name))
                    {
                        cities.Add(city);
                    }
                }

                return cities.Take(10).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов для отелей");
                throw;
            }
        }

        private async Task<int> GetLocationId(string cityName)
        {
            try
            {
                var cities = await SearchHotelCitiesAsync(cityName);
                var city = cities.FirstOrDefault();

                if (city != null && int.TryParse(city.Code, out int locationId))
                {
                    return locationId;
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
