using System.Text.Json;
using System.Text.Json.Serialization;
using TripWise.Models;
using Microsoft.Extensions.Options;

namespace TripWise.Services
{
    public class TravelPayoutsService : IHotelService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TravelPayoutsService> _logger;
        private readonly TravelPayoutsConfig _config;

        public TravelPayoutsService(HttpClient httpClient, ILogger<TravelPayoutsService> logger, IOptions<TravelPayoutsConfig> config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config.Value;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TripWise/1.0");
            _httpClient.DefaultRequestHeaders.Add("X-Access-Token", _config.Token);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Поиск отелей через TravelPayouts API: {@Request}", request);

                // Получаем ID города/местоположения
                var locationId = await GetLocationId(request.City);
                if (string.IsNullOrEmpty(locationId))
                {
                    _logger.LogWarning("❌ Не удалось найти ID для города: {City}", request.City);
                    return new List<Hotel>();
                }

                // Используем основной API TravelPayouts для поиска отелей
                var hotels = await SearchHotelsViaTravelPayouts(request, locationId);

                _logger.LogInformation("🏨 Найдено отелей: {Count}", hotels.Count);
                return hotels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при поиске отелей через TravelPayouts API");
                return new List<Hotel>();
            }
        }

        private async Task<List<Hotel>> SearchHotelsViaTravelPayouts(HotelSearchRequest request, string locationId)
        {
            try
            {
                // Используем Search API от TravelPayouts
                var url = $"{_config.ApiBaseUrl}/v2/hotels/search?" +
                         $"checkIn={request.CheckIn:yyyy-MM-dd}&" +
                         $"checkOut={request.CheckOut:yyyy-MM-dd}&" +
                         $"adults={request.Adults}&" +
                         $"rooms={request.Rooms}&" +
                         $"location={locationId}&" +
                         $"currency=rub&" +
                         $"limit=20&" +
                         $"token={_config.Token}";

                _logger.LogInformation("🌐 Запрос к TravelPayouts Search API: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("📨 Ответ от API: {Json}", json);

                    if (string.IsNullOrWhiteSpace(json) || json == "[]" || json == "null")
                    {
                        _logger.LogWarning("⚠️ API вернул пустой результат");
                        return new List<Hotel>();
                    }

                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<TravelPayoutsSearchResponse>(json);

                        if (apiResponse?.Results?.Hotels != null && apiResponse.Results.Hotels.Any())
                        {
                            _logger.LogInformation("✅ TravelPayouts API вернул {Count} отелей", apiResponse.Results.Hotels.Count);
                            return ConvertTravelPayoutsHotelData(apiResponse.Results.Hotels, request.City);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "❌ Ошибка парсинга JSON от API");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("⚠️ TravelPayouts API вернул ошибку: {StatusCode}, Content: {Error}", response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при запросе к TravelPayouts Search API");
            }

            return new List<Hotel>();
        }

        private List<Hotel> ConvertTravelPayoutsHotelData(List<TravelPayoutsHotel> data, string city)
        {
            var hotels = new List<Hotel>();
            var random = new Random();

            foreach (var item in data)
            {
                try
                {
                    var hotel = new Hotel
                    {
                        Id = item.Id?.ToString() ?? Guid.NewGuid().ToString(),
                        Name = item.Name ?? $"Отель в {city}",
                        Address = item.Address ?? $"{city}, центр",
                        Price = item.Price ?? item.MinPrice ?? random.Next(2000, 8000),
                        Currency = "RUB",
                        Rating = item.Rating ?? (decimal)random.Next(35, 50) / 10,
                        Stars = item.Stars ?? random.Next(3, 5),
                        Description = item.Description ?? $"Комфортабельный отель в {city}",
                        Photos = item.Images?.Any() == true ? item.Images : GenerateHotelPhotos(),
                        Amenities = item.Amenities?.Any() == true ? item.Amenities : GetAmenitiesByStars(item.Stars ?? 3),
                        Location = new Location
                        {
                            City = city,
                            Country = "Россия",
                            Lat = item.Location?.Lat ?? 0,
                            Lng = item.Location?.Lng ?? 0
                        },
                        Provider = "TravelPayouts"
                    };

                    // Добавляем только если есть название
                    if (!string.IsNullOrWhiteSpace(hotel.Name))
                    {
                        hotels.Add(hotel);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка конвертации отеля: {HotelName}", item.Name);
                }
            }

            return hotels.OrderBy(h => h.Price).ToList();
        }

        private async Task<string> GetLocationId(string cityName)
        {
            try
            {
                // Используем Locations API от TravelPayouts
                var url = $"{_config.ApiBaseUrl}/v2/locations/search?" +
                         $"query={Uri.EscapeDataString(cityName)}&" +
                         $"locale=ru&" +
                         $"token={_config.Token}";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var locationsResponse = JsonSerializer.Deserialize<TravelPayoutsLocationsResponse>(json);

                    var city = locationsResponse?.Results?.Locations
                        ?.FirstOrDefault(l => l.Type == "city");

                    if (city != null)
                    {
                        _logger.LogInformation("✅ Найден город: {Name} (ID: {Id})", city.Name, city.Id);
                        return city.Id.ToString();
                    }
                }

                // Если не нашли через API, используем дефолтные коды
                return GetDefaultLocationId(cityName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Ошибка при поиске локации");
                return GetDefaultLocationId(cityName);
            }
        }

        private string GetDefaultLocationId(string cityName)
        {
            var cityMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"москва", "c213"}, {"moscow", "c213"},
                {"санкт-петербург", "c2"}, {"saint petersburg", "c2"}, {"spb", "c2"},
                {"сочи", "c239"}, {"sochi", "c239"},
                {"казань", "c43"}, {"kazan", "c43"},
                {"екатеринбург", "c54"}, {"ekaterinburg", "c54"},
                {"новосибирск", "c65"}, {"novosibirsk", "c65"},
                {"краснодар", "c39"}, {"krasnodar", "c39"},
                {"калининград", "c33"}, {"kaliningrad", "c33"},
                {"владивосток", "c118"}, {"vladivostok", "c118"},
                {"ростов-на-дону", "c79"}, {"rostov-on-don", "c79"},
                {"нижний новгород", "c59"}, {"nizhny novgorod", "c59"},
                {"самара", "c86"}, {"samara", "c86"},
                {"уфа", "c111"}, {"ufa", "c111"},
                {"красноярск", "c38"}, {"krasnoyarsk", "c38"},
                {"пермь", "c73"}, {"perm", "c73"},
                {"воронеж", "c119"}, {"voronezh", "c119"},
                {"волгоград", "c1189"}, {"volgograd", "c1189"}
            };

            return cityMap.GetValueOrDefault(cityName.ToLower(), "c213"); // По умолчанию Москва
        }

        public async Task<List<City>> SearchHotelCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                _logger.LogInformation("🔍 Поиск городов через TravelPayouts: {Query}", query);

                var cities = await SearchCitiesViaTravelPayouts(query) ??
                           GetRussianCities()
                               .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                          c.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
                               .Take(10)
                               .ToList();

                return cities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при поиске городов");
                return new List<City>();
            }
        }

        private async Task<List<City>> SearchCitiesViaTravelPayouts(string query)
        {
            try
            {
                var url = $"{_config.ApiBaseUrl}/v2/locations/search?" +
                         $"query={Uri.EscapeDataString(query)}&" +
                         $"locale=ru&" +
                         $"token={_config.Token}";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var locationsResponse = JsonSerializer.Deserialize<TravelPayoutsLocationsResponse>(json);

                    var cities = locationsResponse?.Results?.Locations
                        ?.Where(l => l.Type == "city")
                        .Select(l => new City
                        {
                            Code = l.Id.ToString(),
                            Name = l.Name,
                            Country = l.CountryName ?? "Россия",
                            Type = "city"
                        })
                        .Take(10)
                        .ToList();

                    return cities;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Ошибка при поиске городов через TravelPayouts");
            }

            return null;
        }

        private List<string> GenerateHotelPhotos()
        {
            return new List<string>
            {
                "https://images.unsplash.com/photo-1551882547-ff40c63fe5fa?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1566073771259-6a8506099945?w=800&h=600&fit=crop"
            };
        }

        private List<string> GetAmenitiesByStars(int stars)
        {
            var amenities = new List<string>
            {
                "Wi-Fi", "Кондиционер", "Телевизор", "Холодильник"
            };

            if (stars >= 4)
            {
                amenities.AddRange(new[] { "Бассейн", "Спа", "Фитнес-центр", "Ресторан" });
            }

            return amenities;
        }

        private List<City> GetRussianCities()
        {
            return new List<City>
            {
                new City { Code = "c213", Name = "Москва", Country = "Россия", Type = "city" },
                new City { Code = "c2", Name = "Санкт-Петербург", Country = "Россия", Type = "city" },
                new City { Code = "c239", Name = "Сочи", Country = "Россия", Type = "city" },
                new City { Code = "c43", Name = "Казань", Country = "Россия", Type = "city" },
                new City { Code = "c54", Name = "Екатеринбург", Country = "Россия", Type = "city" },
                new City { Code = "c65", Name = "Новосибирск", Country = "Россия", Type = "city" },
                new City { Code = "c39", Name = "Краснодар", Country = "Россия", Type = "city" },
                new City { Code = "c33", Name = "Калининград", Country = "Россия", Type = "city" },
                new City { Code = "c118", Name = "Владивосток", Country = "Россия", Type = "city" },
                new City { Code = "c79", Name = "Ростов-на-Дону", Country = "Россия", Type = "city" }
            };
        }
    }

    // Конфигурация
    public class TravelPayoutsConfig
    {
        public string ApiBaseUrl { get; set; } = "https://api.travelpayouts.com";
        public string Marker { get; set; }
        public string Token { get; set; }
    }

    // Модели для TravelPayouts API
    public class TravelPayoutsSearchResponse
    {
        [JsonPropertyName("results")]
        public TravelPayoutsSearchResults Results { get; set; }
    }

    public class TravelPayoutsSearchResults
    {
        [JsonPropertyName("hotels")]
        public List<TravelPayoutsHotel> Hotels { get; set; }
    }

    public class TravelPayoutsHotel
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("min_price")]
        public decimal? MinPrice { get; set; }

        [JsonPropertyName("rating")]
        public decimal? Rating { get; set; }

        [JsonPropertyName("stars")]
        public int? Stars { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("images")]
        public List<string> Images { get; set; }

        [JsonPropertyName("amenities")]
        public List<string> Amenities { get; set; }

        [JsonPropertyName("location")]
        public TravelPayoutsLocation Location { get; set; }
    }

    public class TravelPayoutsLocation
    {
        [JsonPropertyName("lat")]
        public decimal Lat { get; set; }

        [JsonPropertyName("lng")]
        public decimal Lng { get; set; }
    }

    public class TravelPayoutsLocationsResponse
    {
        [JsonPropertyName("results")]
        public TravelPayoutsLocationsResults Results { get; set; }
    }

    public class TravelPayoutsLocationsResults
    {
        [JsonPropertyName("locations")]
        public List<TravelPayoutsLocationItem> Locations { get; set; }
    }

    public class TravelPayoutsLocationItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("country_name")]
        public string CountryName { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}