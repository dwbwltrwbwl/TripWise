using System.Text.Json;
using System.Text.Json.Serialization;
using TripWise.Models;

namespace TripWise.Services
{
    public interface IAviasalesRealService
    {
        Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request);
        Task<List<City>> SearchCitiesAsync(string query);

        // Сделаем эти методы опциональными
        Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request);
        Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0);
        Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId);
    }

    public class AviasalesRealService : IAviasalesRealService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AviasalesRealService> _logger;

        public AviasalesRealService(HttpClient httpClient, ILogger<AviasalesRealService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TripWise/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Поиск рейсов: {DepartureCity} -> {ArrivalCity}",
                    request.DepartureCity, request.ArrivalCity);

                // Получаем коды городов
                var departureCode = await GetCityCode(request.DepartureCity);
                var arrivalCode = await GetCityCode(request.ArrivalCity);

                if (string.IsNullOrEmpty(departureCode) || string.IsNullOrEmpty(arrivalCode))
                {
                    _logger.LogWarning("Не удалось найти коды городов");
                    return new List<Flight>();
                }

                // Используем публичное API для демонстрации
                // В реальном приложении нужно использовать официальное API с ключом
                var flights = await GetDemoFlights(departureCode, arrivalCode, request.DepartureDate);

                return flights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске рейсов");
                return new List<Flight>();
            }
        }

        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                var url = $"https://autocomplete.travelpayouts.com/places2?term={Uri.EscapeDataString(query)}&locale=ru&types[]=airport&types[]=city";

                _logger.LogInformation("Поиск городов: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var citiesData = JsonSerializer.Deserialize<List<CityAutocompleteResponse>>(json);

                    var cities = new List<City>();
                    foreach (var cityData in citiesData ?? new List<CityAutocompleteResponse>())
                    {
                        var city = new City
                        {
                            Code = cityData.Code ?? "",
                            Name = cityData.Name ?? "",
                            Country = cityData.CountryName ?? "",
                            CountryCode = cityData.CountryCode ?? "",
                            Type = cityData.Type ?? ""
                        };

                        if (city.Type == "airport")
                        {
                            city.Airport = city.Name;
                            city.Name = cityData.CityName ?? city.Name;
                        }

                        if (!string.IsNullOrEmpty(city.Code) && !string.IsNullOrEmpty(city.Name))
                        {
                            cities.Add(city);
                        }
                    }

                    return cities.Take(10).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов");
            }

            return new List<City>();
        }

        // Заглушки для остальных методов интерфейса
        public Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request)
        {
            throw new NotImplementedException("Этот метод требует официального API ключа");
        }

        public Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0)
        {
            throw new NotImplementedException("Этот метод требует официального API ключа");
        }

        public Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId)
        {
            throw new NotImplementedException("Этот метод требует официального API ключа");
        }

        private async Task<string> GetCityCode(string cityName)
        {
            try
            {
                var cities = await SearchCitiesAsync(cityName);
                return cities.FirstOrDefault()?.Code ?? ExtractIataCode(cityName);
            }
            catch
            {
                return ExtractIataCode(cityName);
            }
        }

        private async Task<List<Flight>> GetDemoFlights(string departureCode, string arrivalCode, DateTime departureDate)
        {
            // Демо-данные для тестирования
            // В реальном приложении замените на вызов реального API
            var flights = new List<Flight>();

            var airlines = new[] { "Аэрофлот", "S7 Airlines", "UTair", "Победа", "Ural Airlines" };
            var random = new Random();

            for (int i = 0; i < 8; i++)
            {
                var departureTime = departureDate.AddHours(6 + i * 2);
                var flight = new Flight
                {
                    Id = $"FL{i + 1}",
                    Airline = airlines[random.Next(airlines.Length)],
                    FlightNumber = $"{random.Next(100, 999)}",
                    DepartureCity = await GetCityName(departureCode),
                    ArrivalCity = await GetCityName(arrivalCode),
                    DepartureAirport = departureCode,
                    ArrivalAirport = arrivalCode,
                    DepartureTime = departureTime,
                    ArrivalTime = departureTime.AddHours(2 + random.NextDouble() * 4),
                    Price = 5000 + random.Next(0, 20000),
                    Currency = "RUB",
                    Transfers = random.Next(0, 2),
                    Duration = 120 + random.Next(0, 180),
                    Class = "economy"
                };

                flights.Add(flight);
            }

            return flights.OrderBy(f => f.Price).ToList();
        }

        private async Task<string> GetCityName(string code)
        {
            try
            {
                // Пытаемся найти город по коду
                if (code.Length == 3) // IATA код аэропорта
                {
                    var cities = await SearchCitiesAsync(code);
                    return cities.FirstOrDefault()?.Name ?? code;
                }
                return code;
            }
            catch
            {
                return code;
            }
        }

        private string ExtractIataCode(string cityString)
        {
            if (string.IsNullOrEmpty(cityString)) return cityString;

            var match = System.Text.RegularExpressions.Regex.Match(cityString, @"\(([A-Z]{3})\)");
            return match.Success ? match.Groups[1].Value : cityString;
        }
    }

    public class CityAutocompleteResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("country_name")]
        public string CountryName { get; set; }

        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("city_name")]
        public string CityName { get; set; }
    }
}