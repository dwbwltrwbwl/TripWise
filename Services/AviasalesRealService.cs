using System.Text.Json;
using System.Text.Json.Serialization;
using TripWise.Models;

namespace TripWise.Services
{
    public class AviasalesRealService : IAviasalesRealService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AviasalesRealService> _logger;

        public AviasalesRealService(HttpClient httpClient, IConfiguration configuration, ILogger<AviasalesRealService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("=== НАЧАЛО ПОИСКА РЕЙСОВ ===");
                _logger.LogInformation("Запрос: {@Request}", request);

                var departureCode = await GetCityCode(request.DepartureCity);
                var arrivalCode = await GetCityCode(request.ArrivalCity);

                _logger.LogInformation("Коды городов: {Departure} -> {Arrival}", departureCode, arrivalCode);

                if (string.IsNullOrEmpty(departureCode) || string.IsNullOrEmpty(arrivalCode))
                {
                    _logger.LogWarning("Не удалось определить коды городов");
                    return new List<Flight>();
                }

                var flights = await SearchFlightsViaAPI(departureCode, arrivalCode, request.DepartureDate, request.Passengers);

                _logger.LogInformation("=== ПОИСК ЗАВЕРШЕН: {Count} рейсов ===", flights.Count);
                return flights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске авиабилетов");
                // В случае ошибки возвращаем пустой список
                return new List<Flight>();
            }
        }

        private async Task<List<Flight>> SearchFlightsViaAPI(string departureCode, string arrivalCode, DateTime departureDate, int passengers)
        {
            var token = _configuration["TravelPayouts:Token"];

            // Используем API v3 для поиска рейсов по датам
            var url = $"https://api.travelpayouts.com/aviasales/v3/prices_for_dates?" +
                     $"origin={departureCode}&" +
                     $"destination={arrivalCode}&" +
                     $"departure_at={departureDate:yyyy-MM}&" + // Используем месяц для более широкого поиска
                     $"currency=rub&" +
                     $"limit=20&" +
                     $"page=1&" +
                     $"one_way=true&" +
                     $"sorting=price&" +
                     $"token={token}";

            _logger.LogInformation("Запрос к API: {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ошибка API: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return new List<Flight>();
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Ответ API: {Json}", json);

            var apiResponse = JsonSerializer.Deserialize<AviasalesV3Response>(json);

            if (apiResponse?.Data == null)
            {
                _logger.LogWarning("Пустой ответ от API");
                return new List<Flight>();
            }

            return await ConvertV3ResponseToFlights(apiResponse, departureCode, arrivalCode);
        }

        private async Task<List<Flight>> ConvertV3ResponseToFlights(AviasalesV3Response response, string departureCode, string arrivalCode)
        {
            var flights = new List<Flight>();

            // Получаем названия городов один раз
            var departureCityName = await GetCityName(departureCode);
            var arrivalCityName = await GetCityName(arrivalCode);

            foreach (var flightData in response.Data)
            {
                try
                {
                    var flight = new Flight
                    {
                        Id = flightData.FlightNumber ?? Guid.NewGuid().ToString(),
                        Airline = GetAirlineName(flightData.Airline),
                        FlightNumber = flightData.FlightNumber ?? "N/A",
                        DepartureCity = departureCityName,
                        ArrivalCity = arrivalCityName,
                        DepartureAirport = departureCode,
                        ArrivalAirport = arrivalCode,
                        DepartureTime = ParseDateTime(flightData.DepartureAt),
                        ArrivalTime = ParseDateTime(flightData.DepartureAt).AddMinutes(flightData.Duration ?? 120),
                        Price = flightData.Price ?? 0,
                        Currency = "RUB",
                        Transfers = flightData.Transfers ?? 0,
                        Duration = flightData.Duration ?? 120,
                        Class = "economy",
                        IsReturn = false
                    };

                    flights.Add(flight);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при конвертации рейса {FlightNumber}", flightData.FlightNumber);
                }
            }

            return flights.OrderBy(f => f.Price).ToList();
        }

        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                var url = $"https://autocomplete.travelpayouts.com/places2?term={Uri.EscapeDataString(query)}&locale=ru&types[]=airport&types[]=city";

                _logger.LogInformation("Поиск городов: {Query}", query);

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

        // Вспомогательные методы
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

        private async Task<string> GetCityName(string code)
        {
            try
            {
                if (code.Length == 3) // IATA код
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

        private DateTime ParseDateTime(string dateTimeStr)
        {
            if (DateTime.TryParse(dateTimeStr, out var result))
                return result;

            return DateTime.Now.AddDays(1);
        }

        private string GetAirlineName(string airlineCode)
        {
            var airlines = new Dictionary<string, string>
            {
                {"SU", "Аэрофлот"},
                {"S7", "S7 Airlines"},
                {"U6", "Ural Airlines"},
                {"DP", "Победа"},
                {"FV", "Россия"},
                {"6W", "SARATOV AIRLINES"},
                {"5N", "Nordavia"},
                {"D2", "Severstal Air"},
                {"N4", "Nordwind Airlines"},
                {"WZ", "Red Wings"}
            };

            return airlines.ContainsKey(airlineCode) ? airlines[airlineCode] : airlineCode ?? "Неизвестная авиакомпания";
        }

        // Заглушки для неиспользуемых методов интерфейса
        public Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request)
        {
            return Task.FromResult(new AviasalesSearchResponseV2());
        }

        public Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0)
        {
            return Task.FromResult(new AviasalesResultsResponse());
        }

        public Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId)
        {
            return Task.FromResult(new ClickResponseV2());
        }
    }

    // Модели для API v3
    public class AviasalesV3Response
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public List<FlightDataV3> Data { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }
    }

    public class FlightDataV3
    {
        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("origin_airport")]
        public string OriginAirport { get; set; }

        [JsonPropertyName("destination_airport")]
        public string DestinationAirport { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("airline")]
        public string Airline { get; set; }

        [JsonPropertyName("flight_number")]
        public string FlightNumber { get; set; }

        [JsonPropertyName("departure_at")]
        public string DepartureAt { get; set; }

        [JsonPropertyName("return_at")]
        public string ReturnAt { get; set; }

        [JsonPropertyName("transfers")]
        public int? Transfers { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("link")]
        public string Link { get; set; }
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