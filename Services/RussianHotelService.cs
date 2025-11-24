using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using TripWise.Models;

namespace TripWise.Services
{
    public class RussianHotelService : IHotelService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RussianHotelService> _logger;

        public RussianHotelService(HttpClient httpClient, ILogger<RussianHotelService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Поиск отелей через российские API: {@Request}", request);

                // Используем Ostrovok.ru API (работает в России)
                var hotels = await TryOstrovokApi(request) ??
                           await TryTvilApi(request) ??
                           await TryTravelataApi(request);

                _logger.LogInformation($"🏨 Найдено отелей: {hotels?.Count ?? 0}");
                return hotels ?? new List<Hotel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при поиске отелей");
                return new List<Hotel>();
            }
        }

        // 1. Ostrovok.ru API (крупнейший в России)
        private async Task<List<Hotel>> TryOstrovokApi(HotelSearchRequest request)
        {
            try
            {
                // Ostrovok GraphQL API
                var graphqlQuery = new
                {
                    query = @"
                    query SearchHotels($input: HotelSearchInput!) {
                        hotelSearch(input: $input) {
                            hotels {
                                id
                                name
                                address {
                                    full
                                }
                                price {
                                    min
                                    currency
                                }
                                rating {
                                    value
                                }
                                stars
                                photos {
                                    url
                                }
                                amenities {
                                    name
                                }
                                location {
                                    lat
                                    lng
                                    city {
                                        name
                                    }
                                }
                            }
                        }
                    }",
                    variables = new
                    {
                        input = new
                        {
                            location = new
                            {
                                query = request.City
                            },
                            checkIn = request.CheckIn.ToString("yyyy-MM-dd"),
                            checkOut = request.CheckOut.ToString("yyyy-MM-dd"),
                            rooms = new[] { new
                            {
                                adults = request.Adults,
                                children = request.Children > 0 ? new[] { new { age = 10 } } : Array.Empty<object>()
                            }},
                            currency = "RUB",
                            language = "ru"
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(graphqlQuery);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                // Прямой запрос к API Ostrovok
                var response = await _httpClient.PostAsync("https://ostrovok.ru/api/graphql", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OstrovokGraphQLResponse>(json);

                    if (result?.Data?.HotelSearch?.Hotels != null)
                    {
                        _logger.LogInformation("✅ Ostrovok API: найдено {Count} отелей", result.Data.HotelSearch.Hotels.Count);
                        return ConvertOstrovokHotels(result.Data.HotelSearch.Hotels);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Ostrovok API не доступен");
            }

            return null;
        }

        // 2. TVIL.ru API (российский агрегатор)
        private async Task<List<Hotel>> TryTvilApi(HotelSearchRequest request)
        {
            try
            {
                var url = $"https://engine.tvil.ru/api/search/region?" +
                         $"q={HttpUtility.UrlEncode(request.City)}&" +
                         $"checkin={request.CheckIn:yyyy-MM-dd}&" +
                         $"checkout={request.CheckOut:yyyy-MM-dd}&" +
                         $"adults={request.Adults}&" +
                         $"lang=ru";

                _logger.LogInformation("🌐 Запрос к TVIL API: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TvilSearchResponse>(json);

                    if (result?.Hotels != null)
                    {
                        _logger.LogInformation("✅ TVIL API: найдено {Count} отелей", result.Hotels.Count);
                        return ConvertTvilHotels(result.Hotels, request.City);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ TVIL API не доступен");
            }

            return null;
        }

        // 3. Travelata.ru API (популярный в России)
        private async Task<List<Hotel>> TryTravelataApi(HotelSearchRequest request)
        {
            try
            {
                var url = $"https://travelata.ru/api/engine/search/search?" +
                         $"search={HttpUtility.UrlEncode(request.City)}&" +
                         $"fromDate={request.CheckIn:yyyy-MM-dd}&" +
                         $"toDate={request.CheckOut:yyyy-MM-dd}&" +
                         $"adults={request.Adults}&" +
                         $"children={request.Children}";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TravelataResponse>(json);

                    if (result?.Hotels != null)
                    {
                        _logger.LogInformation("✅ Travelata API: найдено {Count} отелей", result.Hotels.Count);
                        return ConvertTravelataHotels(result.Hotels, request.City);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Travelata API не доступен");
            }

            return null;
        }

        // Конвертеры данных
        private List<Hotel> ConvertOstrovokHotels(List<OstrovokHotel> ostrovokHotels)
        {
            var hotels = new List<Hotel>();

            foreach (var hotel in ostrovokHotels.Take(20))
            {
                try
                {
                    var convertedHotel = new Hotel
                    {
                        Id = hotel.Id ?? Guid.NewGuid().ToString(),
                        Name = hotel.Name ?? "Отель",
                        Address = hotel.Address?.Full ?? "Адрес не указан",
                        Price = hotel.Price?.Min ?? 0,
                        Currency = hotel.Price?.Currency ?? "RUB",
                        Rating = hotel.Rating?.Value ?? 0,
                        Stars = hotel.Stars,
                        Description = $"Отель {hotel.Stars} звезд",
                        Photos = hotel.Photos?.Select(p => p.Url).Where(url => !string.IsNullOrEmpty(url)).Take(3).ToList() ?? new List<string>(),
                        Amenities = hotel.Amenities?.Select(a => a.Name).Where(name => !string.IsNullOrEmpty(name)).Take(5).ToList() ?? new List<string>(),
                        Location = new Location
                        {
                            Lat = hotel.Location?.Lat ?? 0,
                            Lng = hotel.Location?.Lng ?? 0,
                            City = hotel.Location?.City?.Name ?? "",
                            Country = "Россия"
                        },
                        Provider = "Ostrovok.ru"
                    };

                    hotels.Add(convertedHotel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка конвертации отеля Ostrovok");
                }
            }

            return hotels.Where(h => h.Price > 0).OrderBy(h => h.Price).ToList();
        }

        private List<Hotel> ConvertTvilHotels(List<TvilHotel> tvilHotels, string city)
        {
            var hotels = new List<Hotel>();

            foreach (var hotel in tvilHotels.Take(20))
            {
                try
                {
                    var convertedHotel = new Hotel
                    {
                        Id = hotel.Id?.ToString() ?? Guid.NewGuid().ToString(),
                        Name = hotel.Name ?? "Отель",
                        Address = hotel.Address ?? $"{city}, центр",
                        Price = hotel.Price > 0 ? hotel.Price : 3000,
                        Rating = hotel.Rating,
                        Stars = hotel.Stars > 0 ? hotel.Stars : 3,
                        Description = hotel.Description ?? $"Отель в {city}",
                        Photos = hotel.Photos?.Take(3).ToList() ?? new List<string>(),
                        Amenities = hotel.Amenities?.Take(5).ToList() ?? new List<string> { "Wi-Fi", "Кондиционер" },
                        Location = new Location
                        {
                            City = city,
                            Country = "Россия"
                        },
                        Provider = "TVIL.ru"
                    };

                    hotels.Add(convertedHotel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка конвертации отеля TVIL");
                }
            }

            return hotels.Where(h => h.Price > 0).OrderBy(h => h.Price).ToList();
        }

        private List<Hotel> ConvertTravelataHotels(List<TravelataHotel> travelataHotels, string city)
        {
            var hotels = new List<Hotel>();

            foreach (var hotel in travelataHotels.Take(20))
            {
                try
                {
                    var convertedHotel = new Hotel
                    {
                        Id = hotel.Id?.ToString() ?? Guid.NewGuid().ToString(),
                        Name = hotel.Name ?? "Отель",
                        Address = hotel.Address ?? $"{city}, курортная зона",
                        Price = hotel.Price > 0 ? hotel.Price : 4000,
                        Rating = hotel.Rating,
                        Stars = hotel.Stars,
                        Description = hotel.Description ?? $"Тур в {city}",
                        Photos = hotel.Photos?.Take(3).ToList() ?? new List<string>(),
                        Amenities = hotel.Amenities ?? new List<string> { "Питание", "Бассейн", "SPA" },
                        Location = new Location
                        {
                            City = city,
                            Country = "Россия"
                        },
                        Provider = "Travelata.ru"
                    };

                    hotels.Add(convertedHotel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка конвертации отеля Travelata");
                }
            }

            return hotels.Where(h => h.Price > 0).OrderBy(h => h.Price).ToList();
        }

        public async Task<List<City>> SearchHotelCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                _logger.LogInformation("🔍 Поиск городов: {Query}", query);

                // Используем комбинированный поиск
                var cities = await SearchViaOstrovok(query) ??
                           await SearchViaTvil(query) ??
                           GetPopularRussianCities(query);

                return cities.Take(10).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при поиске городов");
                return GetPopularRussianCities(query).Take(10).ToList();
            }
        }

        private async Task<List<City>> SearchViaOstrovok(string query)
        {
            try
            {
                var url = $"https://ostrovok.ru/api/suggest/v2/hotel/desktop?" +
                         $"query={HttpUtility.UrlEncode(query)}&" +
                         $"lang=ru";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OstrovokSuggestResponse>(json);

                    var cities = result?.Results?
                        .Where(r => r.Type == "city")
                        .Select(r => new City
                        {
                            Code = r.Id?.ToString(),
                            Name = r.Name ?? "",
                            Country = r.Country ?? "Россия",
                            Type = "city"
                        })
                        .ToList();

                    return cities ?? new List<City>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Ostrovok поиск городов не доступен");
            }

            return null;
        }

        private async Task<List<City>> SearchViaTvil(string query)
        {
            try
            {
                var url = $"https://engine.tvil.ru/api/search/suggest?" +
                         $"q={HttpUtility.UrlEncode(query)}&" +
                         $"lang=ru";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TvilSuggestResponse>(json);

                    var cities = result?.Cities?
                        .Select(c => new City
                        {
                            Code = c.Id?.ToString(),
                            Name = c.Name ?? "",
                            Country = c.Country ?? "Россия",
                            Type = "city"
                        })
                        .ToList();

                    return cities ?? new List<City>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ TVIL поиск городов не доступен");
            }

            return null;
        }

        private List<City> GetPopularRussianCities(string query)
        {
            var allCities = new List<City>
            {
                new City { Code = "moscow", Name = "Москва", Country = "Россия", Type = "city" },
                new City { Code = "saint-petersburg", Name = "Санкт-Петербург", Country = "Россия", Type = "city" },
                new City { Code = "sochi", Name = "Сочи", Country = "Россия", Type = "city" },
                new City { Code = "kazan", Name = "Казань", Country = "Россия", Type = "city" },
                new City { Code = "ekaterinburg", Name = "Екатеринбург", Country = "Россия", Type = "city" },
                new City { Code = "novosibirsk", Name = "Новосибирск", Country = "Россия", Type = "city" },
                new City { Code = "krasnodar", Name = "Краснодар", Country = "Россия", Type = "city" },
                new City { Code = "kaliningrad", Name = "Калининград", Country = "Россия", Type = "city" },
                new City { Code = "vladivostok", Name = "Владивосток", Country = "Россия", Type = "city" },
                new City { Code = "rostov-on-don", Name = "Ростов-на-Дону", Country = "Россия", Type = "city" },
                new City { Code = "ufa", Name = "Уфа", Country = "Россия", Type = "city" },
                new City { Code = "samara", Name = "Самара", Country = "Россия", Type = "city" },
                new City { Code = "omsk", Name = "Омск", Country = "Россия", Type = "city" },
                new City { Code = "chelyabinsk", Name = "Челябинск", Country = "Россия", Type = "city" },
                new City { Code = "volgograd", Name = "Волгоград", Country = "Россия", Type = "city" }
            };

            return allCities
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    // Модели для API ответов
    public class OstrovokGraphQLResponse
    {
        [JsonPropertyName("data")]
        public OstrovokData Data { get; set; }
    }

    public class OstrovokData
    {
        [JsonPropertyName("hotelSearch")]
        public OstrovokHotelSearch HotelSearch { get; set; }
    }

    public class OstrovokHotelSearch
    {
        [JsonPropertyName("hotels")]
        public List<OstrovokHotel> Hotels { get; set; }
    }

    public class OstrovokHotel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public OstrovokAddress Address { get; set; }

        [JsonPropertyName("price")]
        public OstrovokPrice Price { get; set; }

        [JsonPropertyName("rating")]
        public OstrovokRating Rating { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        [JsonPropertyName("photos")]
        public List<OstrovokPhoto> Photos { get; set; }

        [JsonPropertyName("amenities")]
        public List<OstrovokAmenity> Amenities { get; set; }

        [JsonPropertyName("location")]
        public OstrovokLocation Location { get; set; }
    }

    public class OstrovokAddress
    {
        [JsonPropertyName("full")]
        public string Full { get; set; }
    }

    public class OstrovokPrice
    {
        [JsonPropertyName("min")]
        public decimal Min { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }
    }

    public class OstrovokRating
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }
    }

    public class OstrovokPhoto
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class OstrovokAmenity
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class OstrovokLocation
    {
        [JsonPropertyName("lat")]
        public decimal Lat { get; set; }

        [JsonPropertyName("lng")]
        public decimal Lng { get; set; }

        [JsonPropertyName("city")]
        public OstrovokCity City { get; set; }
    }

    public class OstrovokCity
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class TvilSearchResponse
    {
        [JsonPropertyName("hotels")]
        public List<TvilHotel> Hotels { get; set; }
    }

    public class TvilHotel
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        [JsonPropertyName("rating")]
        public decimal Rating { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("photos")]
        public List<string> Photos { get; set; }

        [JsonPropertyName("amenities")]
        public List<string> Amenities { get; set; }
    }

    public class TravelataResponse
    {
        [JsonPropertyName("hotels")]
        public List<TravelataHotel> Hotels { get; set; }
    }

    public class TravelataHotel
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        [JsonPropertyName("rating")]
        public decimal Rating { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("photos")]
        public List<string> Photos { get; set; }

        [JsonPropertyName("amenities")]
        public List<string> Amenities { get; set; }
    }

    public class OstrovokSuggestResponse
    {
        [JsonPropertyName("results")]
        public List<OstrovokSuggestResult> Results { get; set; }
    }

    public class OstrovokSuggestResult
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }

    public class TvilSuggestResponse
    {
        [JsonPropertyName("cities")]
        public List<TvilSuggestCity> Cities { get; set; }
    }

    public class TvilSuggestCity
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }
}