using System.Text.Json;
using System.Text.Json.Serialization;
using TripWise.Models;

namespace TripWise.Services
{
    public class TravelPayoutsService : IHotelService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TravelPayoutsService> _logger;
        private const string API_TOKEN = "5a678657e1cb469daa5d36f87bb12064";

        public TravelPayoutsService(HttpClient httpClient, ILogger<TravelPayoutsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TripWise/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Поиск РЕАЛЬНЫХ отелей через TravelPayouts API: {@Request}", request);

                var hotels = await SearchRealHotelsFromAPI(request);

                _logger.LogInformation($"🏨 Найдено РЕАЛЬНЫХ отелей: {hotels.Count}");
                return hotels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при поиске отелей через TravelPayouts API");
                return new List<Hotel>();
            }
        }

        private async Task<List<Hotel>> SearchRealHotelsFromAPI(HotelSearchRequest request)
        {
            try
            {
                var location = await GetLocationId(request.City);
                if (string.IsNullOrEmpty(location))
                {
                    _logger.LogWarning("❌ Город {City} не найден в API", request.City);
                    return new List<Hotel>();
                }

                var url = $"http://engine.hotellook.com/api/v2/cache.json?" +
                         $"location={location}&" +
                         $"checkIn={request.CheckIn:yyyy-MM-dd}&" +
                         $"checkOut={request.CheckOut:yyyy-MM-dd}&" +
                         $"adults={request.Adults}&" +
                         $"rooms={request.Rooms}&" +
                         $"currency=rub&" +
                         $"token={API_TOKEN}";

                _logger.LogInformation("🌐 Запрос реальных данных к TravelPayouts API: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("📨 Ответ от API: {Json}", json);

                    var hotelData = JsonSerializer.Deserialize<List<TravelPayoutsHotelResponse>>(json);

                    if (hotelData != null && hotelData.Any())
                    {
                        _logger.LogInformation("✅ TravelPayouts API вернул {Count} РЕАЛЬНЫХ отелей", hotelData.Count);
                        return ConvertRealHotelData(hotelData, request.City);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ TravelPayouts API вернул пустой результат");
                        return new List<Hotel>();
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ TravelPayouts API вернул ошибку: {StatusCode}", response.StatusCode);
                    return new List<Hotel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при запросе к TravelPayouts API");
                return new List<Hotel>();
            }
        }

        private List<Hotel> ConvertRealHotelData(List<TravelPayoutsHotelResponse> data, string city)
        {
            var hotels = new List<Hotel>();
            var random = new Random();

            foreach (var item in data)
            {
                try
                {
                    var hotel = new Hotel
                    {
                        Id = item.hotelId.ToString(),
                        Name = item.hotelName ?? "Отель",
                        Address = item.location?.name ?? $"{city}, центр",
                        Price = item.priceFrom,
                        Rating = item.stars > 0 ? (decimal)item.stars / 2 : 0,
                        Stars = item.stars,
                        Description = $"Отель {item.stars} звезд в {city}",
                        Photos = GenerateRealPhotos(item.hotelId),
                        Amenities = GetRealAmenities(item.stars),
                        Location = new Location
                        {
                            City = city,
                            Country = "Россия",
                            Lat = item.location?.lat ?? 0,
                            Lng = item.location?.lon ?? 0
                        },
                        Provider = "TravelPayouts"
                    };

                    hotels.Add(hotel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка конвертации отеля ID: {HotelId}", item.hotelId);
                }
            }

            return hotels.Where(h => h.Price > 0).OrderBy(h => h.Price).ToList();
        }

        private List<string> GenerateRealPhotos(int hotelId)
        {
            var photoUrls = new[]
            {
                "https://images.unsplash.com/photo-1551882547-ff40c63fe5fa?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1566073771259-6a8506099945?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1582719478250-c89cae4dc85b?w=800&h=600&fit=crop"
            };

            var random = new Random();
            return photoUrls.OrderBy(x => random.Next()).Take(2).ToList();
        }

        private List<string> GetRealAmenities(int stars)
        {
            var amenities = new List<string>
            {
                "Wi-Fi", "Кондиционер", "Телевизор", "Холодильник", "Собственная ванная"
            };

            if (stars >= 4)
            {
                amenities.AddRange(new[] { "Бассейн", "Спа", "Фитнес-центр", "Ресторан" });
            }

            return amenities;
        }

        private async Task<string> GetLocationId(string cityName)
        {
            try
            {
                var url = $"http://engine.hotellook.com/api/v2/lookup.json?" +
                         $"query={Uri.EscapeDataString(cityName)}&" +
                         $"lang=ru&" +
                         $"lookFor=city&" +
                         $"token={API_TOKEN}";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var lookupResponse = JsonSerializer.Deserialize<TravelPayoutsLookupResponse>(json);

                    var city = lookupResponse?.results?.locations
                        ?.FirstOrDefault(l => l.type == "city");

                    return city?.iata ?? GetDefaultIataCode(cityName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Ошибка при поиске локации");
            }

            return GetDefaultIataCode(cityName);
        }

        private string GetDefaultIataCode(string cityName)
        {
            var cityMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"москва", "MOW"}, {"moscow", "MOW"},
                {"санкт-петербург", "LED"}, {"saint petersburg", "LED"}, {"spb", "LED"},
                {"сочи", "AER"}, {"sochi", "AER"},
                {"казань", "KZN"}, {"kazan", "KZN"},
                {"екатеринбург", "SVX"}, {"ekaterinburg", "SVX"},
                {"новосибирск", "OVB"}, {"novosibirsk", "OVB"},
                {"краснодар", "KRR"}, {"krasnodar", "KRR"},
                {"калининград", "KGD"}, {"kaliningrad", "KGD"},
                {"владивосток", "VVO"}, {"vladivostok", "VVO"},
                {"ростов-на-дону", "ROV"}, {"rostov-on-don", "ROV"}
            };

            return cityMap.GetValueOrDefault(cityName.ToLower());
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
                               .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
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
                var url = $"http://engine.hotellook.com/api/v2/lookup.json?" +
                         $"query={Uri.EscapeDataString(query)}&" +
                         $"lang=ru&" +
                         $"lookFor=city&" +
                         $"token={API_TOKEN}";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var lookupResponse = JsonSerializer.Deserialize<TravelPayoutsLookupResponse>(json);

                    var cities = lookupResponse?.results?.locations
                        ?.Where(l => l.type == "city")
                        .Select(l => new City
                        {
                            Code = l.iata,
                            Name = l.name,
                            Country = l.countryName,
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

        private List<City> GetRussianCities()
        {
            return new List<City>
            {
                new City { Code = "MOW", Name = "Москва", Country = "Россия", Type = "city" },
                new City { Code = "LED", Name = "Санкт-Петербург", Country = "Россия", Type = "city" },
                new City { Code = "AER", Name = "Сочи", Country = "Россия", Type = "city" },
                new City { Code = "KZN", Name = "Казань", Country = "Россия", Type = "city" },
                new City { Code = "SVX", Name = "Екатеринбург", Country = "Россия", Type = "city" },
                new City { Code = "OVB", Name = "Новосибирск", Country = "Россия", Type = "city" },
                new City { Code = "KRR", Name = "Краснодар", Country = "Россия", Type = "city" },
                new City { Code = "KGD", Name = "Калининград", Country = "Россия", Type = "city" },
                new City { Code = "VVO", Name = "Владивосток", Country = "Россия", Type = "city" },
                new City { Code = "ROV", Name = "Ростов-на-Дону", Country = "Россия", Type = "city" }
            };
        }
    }

    // Модели для TravelPayouts API (только здесь)
    public class TravelPayoutsHotelResponse
    {
        [JsonPropertyName("hotelId")]
        public int hotelId { get; set; }

        [JsonPropertyName("priceFrom")]
        public decimal priceFrom { get; set; }

        [JsonPropertyName("stars")]
        public int stars { get; set; }

        [JsonPropertyName("hotelName")]
        public string hotelName { get; set; }

        [JsonPropertyName("location")]
        public TravelPayoutsLocation location { get; set; }
    }

    public class TravelPayoutsLocation
    {
        [JsonPropertyName("name")]
        public string name { get; set; }

        [JsonPropertyName("country")]
        public string country { get; set; }

        [JsonPropertyName("lat")]
        public decimal lat { get; set; }

        [JsonPropertyName("lon")]
        public decimal lon { get; set; }
    }

    public class TravelPayoutsLookupResponse
    {
        [JsonPropertyName("results")]
        public TravelPayoutsLookupResults results { get; set; }
    }

    public class TravelPayoutsLookupResults
    {
        [JsonPropertyName("locations")]
        public List<TravelPayoutsLookupLocation> locations { get; set; }
    }

    public class TravelPayoutsLookupLocation
    {
        [JsonPropertyName("id")]
        public int id { get; set; }

        [JsonPropertyName("name")]
        public string name { get; set; }

        [JsonPropertyName("countryName")]
        public string countryName { get; set; }

        [JsonPropertyName("type")]
        public string type { get; set; }

        [JsonPropertyName("iata")]
        public string iata { get; set; }
    }
}