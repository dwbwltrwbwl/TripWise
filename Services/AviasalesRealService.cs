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
        private readonly string _apiToken;

        public AviasalesRealService(HttpClient httpClient, IConfiguration configuration, ILogger<AviasalesRealService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiToken = _configuration["TravelPayouts:Token"];

            if (string.IsNullOrEmpty(_apiToken))
            {
                _logger.LogWarning("TravelPayouts token не настроен в конфигурации!");
            }

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Увеличиваем таймаут для API
        }

        public async Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("=== ПОИСК РЕАЛЬНЫХ РЕЙСОВ ===");
                _logger.LogInformation("Маршрут: {DepartureCity} → {ArrivalCity}", request.DepartureCity, request.ArrivalCity);
                _logger.LogInformation("Даты: {DepartureDate} - {ReturnDate}",
                    request.DepartureDate.ToString("yyyy-MM-dd"),
                    request.ReturnDate?.ToString("yyyy-MM-dd") ?? "без обратного");

                // Получаем IATA коды
                var departureCode = await GetCityCode(request.DepartureCity);
                var arrivalCode = await GetCityCode(request.ArrivalCity);

                if (string.IsNullOrEmpty(departureCode) || string.IsNullOrEmpty(arrivalCode))
                {
                    _logger.LogWarning("Не удалось определить IATA коды для {DepartureCity} или {ArrivalCity}",
                        request.DepartureCity, request.ArrivalCity);
                    return new List<Flight>();
                }

                _logger.LogInformation("IATA коды: {DepartureCode} → {ArrivalCode}", departureCode, arrivalCode);

                var allFlights = new List<Flight>();

                // 1. Рейсы ТУДА
                _logger.LogInformation("Поиск рейсов ТУДА на {DepartureDate}...", request.DepartureDate.ToString("yyyy-MM-dd"));
                var oneWayFlights = await SearchFlightsForRoute(
                    departureCode,
                    arrivalCode,
                    request.DepartureDate,
                    isReturn: false,
                    request.Passengers);

                allFlights.AddRange(oneWayFlights);
                _logger.LogInformation("Найдено рейсов ТУДА: {Count}", oneWayFlights.Count);

                // 2. Рейсы ОБРАТНО (только если указана обратная дата)
                if (request.ReturnDate.HasValue && request.ReturnDate.Value > DateTime.MinValue)
                {
                    _logger.LogInformation("Поиск рейсов ОБРАТНО на {ReturnDate}...", request.ReturnDate.Value.ToString("yyyy-MM-dd"));
                    var returnFlights = await SearchFlightsForRoute(
                        arrivalCode,
                        departureCode,
                        request.ReturnDate.Value,
                        isReturn: true,
                        request.Passengers);

                    allFlights.AddRange(returnFlights);
                    _logger.LogInformation("Найдено рейсов ОБРАТНО: {Count}", returnFlights.Count);
                }
                else
                {
                    _logger.LogInformation("Обратная дата не указана - генерируем только рейсы туда");
                }

                // Логируем статистику по найденным рейсам
                LogFlightStatistics(allFlights);

                return allFlights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске реальных рейсов");
                return new List<Flight>();
            }
        }

        private async Task<List<Flight>> SearchFlightsForRoute(string origin, string destination, DateTime date, bool isReturn, int passengers)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiToken))
                {
                    _logger.LogError("Токен TravelPayouts не настроен");
                    return new List<Flight>();
                }

                // Используем API v3 для получения реальных цен и рейсов
                // Параметры:
                // - origin: IATA код города отправления
                // - destination: IATA код города назначения
                // - departure_at: конкретная дата вылета
                // - currency: валюта (рубли)
                // - limit: максимальное количество результатов
                // - token: токен API
                var url = $"https://api.travelpayouts.com/aviasales/v3/prices_for_dates?" +
                         $"origin={origin}&" +
                         $"destination={destination}&" +
                         $"departure_at={date:yyyy-MM-dd}&" +
                         $"currency=rub&" +
                         $"limit=50&" + // Увеличиваем лимит для получения больше рейсов
                         $"one_way=true&" +
                         $"sorting=price&" +
                         $"token={_apiToken}";

                _logger.LogInformation("Запрос к API: {Url}", url.Replace(_apiToken, "***"));

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<AviasalesV3Response>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse == null || !apiResponse.Success || apiResponse.Data == null || apiResponse.Data.Count == 0)
                {
                    _logger.LogInformation("API не вернул рейсы для {Origin}-{Destination} на {Date}",
                        origin, destination, date.ToString("yyyy-MM-dd"));

                    // Попробуем поискать с небольшой вариацией даты (±2 дня)
                    return await SearchWithDateVariation(origin, destination, date, isReturn);
                }

                _logger.LogInformation("API вернул {Count} рейсов для {Origin}-{Destination} на {Date}",
                    apiResponse.Data.Count, origin, destination, date.ToString("yyyy-MM-dd"));

                // Получаем названия городов для отображения
                var originCity = await GetCityName(origin);
                var destinationCity = await GetCityName(destination);

                var flights = new List<Flight>();
                int flightCounter = 0;

                foreach (var flightData in apiResponse.Data)
                {
                    try
                    {
                        // Проверяем обязательные поля
                        if (string.IsNullOrEmpty(flightData.Airline) ||
                            string.IsNullOrEmpty(flightData.FlightNumber) ||
                            string.IsNullOrEmpty(flightData.DepartureAt) ||
                            flightData.Price == null || flightData.Price <= 0)
                        {
                            continue;
                        }

                        // Парсим дату вылета
                        if (!DateTime.TryParse(flightData.DepartureAt, out var departureTime))
                        {
                            continue;
                        }

                        // Проверяем, что рейс на нужную дату (±1 день для гибкости)
                        if (Math.Abs((departureTime.Date - date.Date).Days) > 1)
                        {
                            _logger.LogDebug("Рейс на другую дату: {FlightDate} vs {SearchDate}",
                                departureTime.ToString("yyyy-MM-dd"), date.ToString("yyyy-MM-dd"));
                            continue;
                        }

                        // Рассчитываем время прибытия
                        var arrivalTime = CalculateArrivalTime(departureTime, flightData.Duration, origin, destination);

                        // Рассчитываем окончательную цену с учетом количества пассажиров
                        var totalPrice = flightData.Price.Value * passengers;

                        var flight = new Flight
                        {
                            Id = $"{flightData.FlightNumber}-{departureTime:yyyyMMddHHmm}-{flightCounter++}",
                            Airline = GetAirlineName(flightData.Airline),
                            FlightNumber = flightData.FlightNumber,
                            DepartureCity = originCity,
                            ArrivalCity = destinationCity,
                            DepartureAirport = origin,
                            ArrivalAirport = destination,
                            DepartureTime = departureTime,
                            ArrivalTime = arrivalTime,
                            Price = totalPrice,
                            Currency = apiResponse.Currency ?? "RUB",
                            Transfers = flightData.Transfers ?? 0,
                            Duration = flightData.Duration ?? CalculateDuration(origin, destination),
                            Class = "economy",
                            IsReturn = isReturn
                        };

                        flights.Add(flight);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Ошибка обработки рейса {FlightNumber}", flightData.FlightNumber);
                    }
                }

                // Сортируем по времени вылета
                var sortedFlights = flights
                    .OrderBy(f => f.DepartureTime)
                    .ToList();

                _logger.LogInformation("Обработано рейсов для {Origin}-{Destination}: {Count} из {Total}",
                    origin, destination, sortedFlights.Count, apiResponse.Data.Count);

                return sortedFlights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка поиска рейсов для {Origin}-{Destination} на {Date}",
                    origin, destination, date.ToString("yyyy-MM-dd"));
                return new List<Flight>();
            }
        }

        private async Task<List<Flight>> SearchWithDateVariation(string origin, string destination, DateTime originalDate, bool isReturn)
        {
            var allFlights = new List<Flight>();

            // Проверяем рейсы за 3 дня в обе стороны от оригинальной даты
            var datesToCheck = new List<DateTime>
            {
                originalDate.AddDays(-2),
                originalDate.AddDays(-1),
                originalDate,
                originalDate.AddDays(1),
                originalDate.AddDays(2)
            };

            foreach (var date in datesToCheck)
            {
                try
                {
                    var url = $"https://api.travelpayouts.com/aviasales/v3/prices_for_dates?" +
                             $"origin={origin}&" +
                             $"destination={destination}&" +
                             $"departure_at={date:yyyy-MM-dd}&" +
                             $"currency=rub&" +
                             $"one_way=true&" +
                             $"sorting=price&" +
                             $"token={_apiToken}";

                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<AviasalesV3Response>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse?.Data == null || !apiResponse.Success || apiResponse.Data.Count == 0) continue;

                    var originCity = await GetCityName(origin);
                    var destinationCity = await GetCityName(destination);

                    foreach (var flightData in apiResponse.Data.Take(5)) // Берем только первые 5 рейсов с каждой даты
                    {
                        try
                        {
                            if (!DateTime.TryParse(flightData.DepartureAt, out var departureTime) ||
                                flightData.Price == null || flightData.Price <= 0)
                                continue;

                            var arrivalTime = CalculateArrivalTime(departureTime, flightData.Duration, origin, destination);

                            var flight = new Flight
                            {
                                Id = $"{flightData.FlightNumber}-{departureTime:yyyyMMddHHmm}-{Guid.NewGuid().ToString()[..8]}",
                                Airline = GetAirlineName(flightData.Airline),
                                FlightNumber = flightData.FlightNumber ?? "Unknown",
                                DepartureCity = originCity,
                                ArrivalCity = destinationCity,
                                DepartureAirport = origin,
                                ArrivalAirport = destination,
                                DepartureTime = departureTime,
                                ArrivalTime = arrivalTime,
                                Price = flightData.Price.Value,
                                Currency = apiResponse.Currency ?? "RUB",
                                Transfers = flightData.Transfers ?? 0,
                                Duration = flightData.Duration ?? CalculateDuration(origin, destination),
                                Class = "economy",
                                IsReturn = isReturn
                            };

                            allFlights.Add(flight);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Ошибка обработки вариативного рейса");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Ошибка при поиске вариативных рейсов на {Date}", date.ToString("yyyy-MM-dd"));
                }
            }

            _logger.LogInformation("Найдено рейсов с вариацией дат: {Count}", allFlights.Count);
            return allFlights;
        }

        private DateTime CalculateArrivalTime(DateTime departureTime, int? durationMinutes, string origin, string destination)
        {
            if (durationMinutes.HasValue && durationMinutes.Value > 0)
            {
                return departureTime.AddMinutes(durationMinutes.Value);
            }

            // Если продолжительность не указана, используем примерные значения
            var estimatedDuration = CalculateDuration(origin, destination);
            return departureTime.AddMinutes(estimatedDuration);
        }

        private int CalculateDuration(string origin, string destination)
        {
            // Примерная продолжительность полета между городами (в минутах)
            // В реальной системе это бы бралось из базы данных или кэша
            var durations = new Dictionary<string, int>
            {
                {"MOW-LED", 90},    // Москва - СПб
                {"MOW-AER", 150},   // Москва - Сочи
                {"MOW-KZN", 105},   // Москва - Казань
                {"MOW-SVX", 180},   // Москва - Екатеринбург
                {"MOW-OVB", 240},   // Москва - Новосибирск
                {"LED-AER", 180},   // СПб - Сочи
                {"LED-KZN", 120},   // СПб - Казань
                {"LED-SVX", 210},   // СПб - Екатеринбург
                {"AER-KZN", 120},   // Сочи - Казань
                {"KZN-SVX", 120},   // Казань - Екатеринбург
            };

            var key = $"{origin}-{destination}";
            var reverseKey = $"{destination}-{origin}";

            if (durations.ContainsKey(key))
                return durations[key];
            if (durations.ContainsKey(reverseKey))
                return durations[reverseKey];

            return 120; // По умолчанию 2 часа
        }

        private void LogFlightStatistics(List<Flight> flights)
        {
            _logger.LogInformation("=== СТАТИСТИКА ПО РЕЙСАМ ===");
            _logger.LogInformation("Всего рейсов: {TotalCount}", flights.Count);
            _logger.LogInformation("Рейсы туда: {OneWayCount}", flights.Count(f => !f.IsReturn));
            _logger.LogInformation("Рейсы обратно: {ReturnCount}", flights.Count(f => f.IsReturn));

            // Группируем по авиакомпаниям
            var airlines = flights
                .GroupBy(f => f.Airline)
                .Select(g => new { Airline = g.Key, Count = g.Count() })
                .OrderByDescending(a => a.Count);

            foreach (var airline in airlines)
            {
                _logger.LogInformation("  {Airline}: {Count} рейсов", airline.Airline, airline.Count);
            }

            // Анализируем количество пересадок
            var directFlights = flights.Count(f => f.Transfers == 0);
            var transferFlights = flights.Count(f => f.Transfers > 0);
            _logger.LogInformation("Прямых рейсов: {DirectCount}", directFlights);
            _logger.LogInformation("С пересадками: {TransferCount}", transferFlights);

            // Диапазон цен
            if (flights.Any())
            {
                var minPrice = flights.Min(f => f.Price);
                var maxPrice = flights.Max(f => f.Price);
                var avgPrice = flights.Average(f => f.Price);
                _logger.LogInformation("Цены: от {MinPrice} до {MaxPrice} (средняя: {AvgPrice})",
                    minPrice, maxPrice, (int)avgPrice);
            }
        }

        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                var url = $"https://autocomplete.travelpayouts.com/places2?" +
                         $"term={Uri.EscapeDataString(query)}&" +
                         $"locale=ru&" +
                         $"types[]=airport&" +
                         $"types[]=city";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var citiesData = JsonSerializer.Deserialize<List<CityAutocompleteResponse>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var cities = new List<City>();
                foreach (var cityData in citiesData ?? new List<CityAutocompleteResponse>())
                {
                    if (string.IsNullOrEmpty(cityData.Code) || string.IsNullOrEmpty(cityData.Name))
                        continue;

                    var city = new City
                    {
                        Code = cityData.Code,
                        Name = cityData.Name,
                        Country = cityData.CountryName ?? "",
                        CountryCode = cityData.CountryCode ?? "",
                        Type = cityData.Type ?? "city"
                    };

                    if (city.Type == "airport" && !string.IsNullOrEmpty(cityData.CityName))
                    {
                        city.Airport = city.Name;
                        city.Name = cityData.CityName;
                    }

                    cities.Add(city);
                }

                return cities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов для запроса: {Query}", query);
                return new List<City>();
            }
        }

        private async Task<string> GetCityCode(string cityName)
        {
            try
            {
                if (string.IsNullOrEmpty(cityName))
                    return "";

                // Если уже IATA код (3 буквы)
                if (cityName.Length == 3 && cityName.All(char.IsLetter))
                    return cityName.ToUpper();

                // Извлекаем код из скобок, если есть
                var match = System.Text.RegularExpressions.Regex.Match(cityName, @"\(([A-Z]{3})\)");
                if (match.Success)
                    return match.Groups[1].Value;

                // Ищем через API
                var cities = await SearchCitiesAsync(cityName.Split('(')[0].Trim());
                return cities.FirstOrDefault()?.Code ?? "";
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> GetCityName(string code)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                    return code;

                if (code.Length == 3 && code.All(char.IsLetter))
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

        private string GetAirlineName(string airlineCode)
        {
            var airlines = new Dictionary<string, string>
            {
                {"SU", "Аэрофлот"}, {"S7", "S7 Airlines"}, {"U6", "Ural Airlines"},
                {"DP", "Победа"}, {"FV", "Россия"}, {"5N", "Nordavia"},
                {"N4", "Nordwind Airlines"}, {"WZ", "Red Wings"}, {"B2", "Белавиа"},
                {"TK", "Turkish Airlines"}, {"LH", "Lufthansa"}, {"AF", "Air France"},
                {"KL", "KLM"}, {"AY", "Finnair"}, {"LO", "LOT Polish Airlines"},
                {"PC", "Pegasus Airlines"}, {"FR", "Ryanair"}, {"W6", "Wizz Air"},
                {"U2", "easyJet"}, {"BT", "Air Baltic"}, {"JU", "Air Serbia"},
                {"EK", "Emirates"}, {"QR", "Qatar Airways"}, {"CX", "Cathay Pacific"},
                {"BA", "British Airways"}, {"AA", "American Airlines"}, {"DL", "Delta Air Lines"},
                {"UA", "United Airlines"}, {"AC", "Air Canada"}, {"QF", "Qantas"},
                {"JL", "Japan Airlines"}, {"NH", "ANA"}, {"OZ", "Asiana Airlines"},
                {"KE", "Korean Air"}, {"CI", "China Airlines"}, {"BR", "EVA Air"},
                {"TG", "Thai Airways"}, {"SQ", "Singapore Airlines"}, {"MH", "Malaysia Airlines"},
                {"EY", "Etihad Airways"}, {"GF", "Gulf Air"}, {"FZ", "FlyDubai"},
                {"J2", "Azerbaijan Airlines"}, {"KC", "Air Astana"}, {"HY", "Uzbekistan Airways"}
            };

            return airlines.ContainsKey(airlineCode) ? airlines[airlineCode] : airlineCode ?? "Авиакомпания";
        }

        // Заглушки для неиспользуемых методов
        public Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request)
            => Task.FromResult(new AviasalesSearchResponseV2());

        public Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0)
            => Task.FromResult(new AviasalesResultsResponse());

        public Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId)
            => Task.FromResult(new ClickResponseV2());
    }

    // Модели для API
    public class AviasalesV3Response
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public List<FlightDataV3> Data { get; set; } = new();

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "RUB";

        [JsonPropertyName("total_pages")]
        public int? TotalPages { get; set; }
    }

    public class FlightDataV3
    {
        [JsonPropertyName("origin")]
        public string Origin { get; set; } = "";

        [JsonPropertyName("destination")]
        public string Destination { get; set; } = "";

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("airline")]
        public string Airline { get; set; } = "";

        [JsonPropertyName("flight_number")]
        public string FlightNumber { get; set; } = "";

        [JsonPropertyName("departure_at")]
        public string DepartureAt { get; set; } = "";

        [JsonPropertyName("transfers")]
        public int? Transfers { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("link")]
        public string Link { get; set; } = "";
    }

    public class CityAutocompleteResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("country_name")]
        public string CountryName { get; set; } = "";

        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("city_name")]
        public string CityName { get; set; } = "";
    }
}