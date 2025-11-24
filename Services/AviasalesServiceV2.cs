using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TripWise.Models;

namespace TripWise.Services
{
    public interface IAviasalesRealService
    {
        Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request);
        Task<List<City>> SearchCitiesAsync(string query);
        Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request);
        Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0);
        Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId);
    }

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

            // Настройка HttpClient
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Запуск поиска рейсов: {DepartureCity} -> {ArrivalCity}",
                    request.DepartureCity, request.ArrivalCity);

                // Запускаем поиск
                var searchResponse = await StartSearchAsync(request);

                if (searchResponse == null || string.IsNullOrEmpty(searchResponse.SearchId))
                {
                    _logger.LogError("Не удалось получить search_id от API");
                    return new List<Flight>();
                }

                _logger.LogInformation("Поиск запущен, SearchId: {SearchId}, ожидание результатов...", searchResponse.SearchId);

                // Ждем некоторое время для сбора результатов (увеличим время ожидания)
                await Task.Delay(10000);

                // Получаем результаты
                var results = await GetSearchResultsAsync(searchResponse.SearchId, searchResponse.ResultsUrl);

                if (results == null)
                {
                    _logger.LogWarning("Не удалось получить результаты поиска");
                    return new List<Flight>();
                }

                _logger.LogInformation("Получены результаты: {TicketsCount} билетов", results.Tickets?.Count ?? 0);

                // Конвертируем в нашу модель
                return ConvertToFlights(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка при поиске авиабилетов");
                throw;
            }
        }

        public async Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request)
        {
            try
            {
                var marker = _configuration["TravelPayouts:Marker"];
                var token = _configuration["TravelPayouts:Token"];
                var baseUrl = _configuration["TravelPayouts:ApiBaseUrl"];

                if (string.IsNullOrEmpty(marker) || string.IsNullOrEmpty(token))
                {
                    throw new Exception("Не настроены учетные данные TravelPayouts. Проверьте appsettings.json");
                }

                _logger.LogInformation("Используем маркер: {Marker}, токен: {Token}", marker, token);

                var searchRequest = new AviasalesSearchRequestV2
                {
                    Marker = marker,
                    Locale = "ru",
                    CurrencyCode = "RUB",
                    MarketCode = "ru",
                    SearchParams = new SearchParams
                    {
                        TripClass = MapClassToTripClass(request.Class),
                        Passengers = new Passengers
                        {
                            Adults = request.Passengers,
                            Children = 0,
                            Infants = 0
                        },
                        Directions = CreateDirections(request)
                    }
                };

                var signature = GenerateSignature(token, marker, searchRequest);

                // Используем упрощенные заголовки для начала
                var url = $"{baseUrl}/v2/prices/search/affiliate/start";

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var jsonContent = JsonSerializer.Serialize(searchRequest, jsonOptions);
                _logger.LogDebug("Отправляемый JSON: {Json}", jsonContent);

                var startRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                // Добавляем обязательные заголовки
                startRequest.Headers.Add("X-Access-Token", token);
                startRequest.Headers.Add("X-Signature", signature);
                startRequest.Headers.Add("Accept", "application/json");

                _logger.LogInformation("Отправка запроса к: {Url}", url);

                var startResponse = await _httpClient.SendAsync(startRequest);

                _logger.LogInformation("Получен ответ: {StatusCode}", startResponse.StatusCode);

                if (!startResponse.IsSuccessStatusCode)
                {
                    var errorContent = await startResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Ошибка API: {StatusCode}, Response: {ErrorContent}",
                        startResponse.StatusCode, errorContent);
                    throw new HttpRequestException($"API error: {startResponse.StatusCode} - {errorContent}");
                }

                var startContent = await startResponse.Content.ReadAsStringAsync();
                _logger.LogDebug("Ответ API: {Response}", startContent);

                var searchResponse = JsonSerializer.Deserialize<AviasalesSearchResponseV2>(startContent);

                if (string.IsNullOrEmpty(searchResponse?.SearchId))
                {
                    _logger.LogError("Неверный ответ от API: {Response}", startContent);
                    throw new Exception("Не удалось получить search_id от API");
                }

                return searchResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при старте поиска авиабилетов");
                throw;
            }
        }

        public async Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0)
        {
            try
            {
                var token = _configuration["TravelPayouts:Token"];

                if (string.IsNullOrEmpty(searchId))
                {
                    throw new ArgumentException("SearchId не может быть пустым");
                }

                // Формируем URL для получения результатов
                var url = $"{resultsUrl}?search_id={Uri.EscapeDataString(searchId)}&limit=50";

                if (lastUpdateTimestamp > 0)
                {
                    url += $"&last_update_timestamp={lastUpdateTimestamp}";
                }

                _logger.LogInformation("Запрос результатов по URL: {Url}", url);

                var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                requestMessage.Headers.Add("X-Access-Token", token);
                requestMessage.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(requestMessage);

                _logger.LogInformation("Статус ответа результатов: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Ошибка при получении результатов: {StatusCode}, Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Ответ результатов: {Response}", json);

                var results = JsonSerializer.Deserialize<AviasalesResultsResponse>(json);

                if (results == null)
                {
                    _logger.LogWarning("Не удалось десериализовать ответ результатов");
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении результатов поиска");
                return null;
            }
        }

        public async Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId)
        {
            try
            {
                var token = _configuration["TravelPayouts:Token"];
                var marker = _configuration["TravelPayouts:Marker"];

                var clickRequest = new
                {
                    search_id = searchId,
                    proposal_id = proposalId,
                    marker = marker
                };

                var signature = GenerateSignature(token, marker, clickRequest);
                var url = $"{resultsUrl}/v2/prices/search/affiliate/click";

                var jsonContent = JsonSerializer.Serialize(clickRequest);
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                requestMessage.Headers.Add("X-Access-Token", token);
                requestMessage.Headers.Add("X-Signature", signature);

                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ClickResponseV2>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении ссылки на бронирование");
                throw;
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

                    return cities;
                }
                else
                {
                    _logger.LogWarning("Ошибка при поиске городов: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов для запроса: {Query}", query);
            }

            return new List<City>();
        }

        // Вспомогательные методы

        private List<Direction> CreateDirections(FlightSearchRequest request)
        {
            var directions = new List<Direction>
            {
                new Direction
                {
                    Origin = ExtractIataCode(request.DepartureCity),
                    Destination = ExtractIataCode(request.ArrivalCity),
                    Date = request.DepartureDate.ToString("yyyy-MM-dd")
                }
            };

            if (request.ReturnDate.HasValue && request.TripType == "round")
            {
                directions.Add(new Direction
                {
                    Origin = ExtractIataCode(request.ArrivalCity),
                    Destination = ExtractIataCode(request.DepartureCity),
                    Date = request.ReturnDate.Value.ToString("yyyy-MM-dd")
                });
            }

            return directions;
        }

        private List<Flight> ConvertToFlights(AviasalesResultsResponse response)
        {
            var flights = new List<Flight>();

            if (response?.Tickets == null)
            {
                _logger.LogWarning("Нет билетов в ответе для конвертации");
                return flights;
            }

            _logger.LogInformation("Конвертация {TicketsCount} билетов", response.Tickets.Count);

            foreach (var ticket in response.Tickets.Take(20)) // Ограничиваем количество
            {
                try
                {
                    var cheapestProposal = ticket.Proposals?.OrderBy(p => p.Price?.Amount ?? decimal.MaxValue).FirstOrDefault();
                    if (cheapestProposal == null) continue;

                    // Находим первый сегмент для основной информации
                    var firstSegment = ticket.Segments?.FirstOrDefault();
                    if (firstSegment?.Flights == null || !firstSegment.Flights.Any()) continue;

                    var firstFlightIndex = firstSegment.Flights.First();
                    if (firstFlightIndex >= response.FlightLegs.Count) continue;

                    var flightLeg = response.FlightLegs[firstFlightIndex];

                    var flight = new Flight
                    {
                        Id = ticket.Id ?? Guid.NewGuid().ToString(),
                        Airline = GetAirlineName(response.Airlines, flightLeg.OperatingCarrierDesignator),
                        FlightNumber = flightLeg.OperatingCarrierDesignator ?? "N/A",
                        DepartureCity = GetCityName(response.Airports, flightLeg.Origin),
                        ArrivalCity = GetCityName(response.Airports, flightLeg.Destination),
                        DepartureAirport = flightLeg.Origin ?? "N/A",
                        ArrivalAirport = flightLeg.Destination ?? "N/A",
                        DepartureTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.DepartureUnixTimestamp).DateTime,
                        ArrivalTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.ArrivalUnixTimestamp).DateTime,
                        Price = cheapestProposal.Price?.Amount ?? 0,
                        Duration = (int)(flightLeg.ArrivalUnixTimestamp - flightLeg.DepartureUnixTimestamp) / 60,
                        Transfers = ticket.Segments?.Sum(s => s.Transfers?.Count ?? 0) ?? 0,
                        Currency = cheapestProposal.Price?.Currency ?? "RUB",
                        Class = MapTripClassToClass(response.SearchParams?.TripClass ?? "Y")
                    };

                    flights.Add(flight);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при конвертации билета {TicketId}", ticket.Id);
                }
            }

            _logger.LogInformation("Успешно сконвертировано {FlightsCount} рейсов", flights.Count);

            return flights.OrderBy(f => f.Price).ToList();
        }

        private string GetAirlineName(Dictionary<string, Airline> airlines, string designator)
        {
            if (string.IsNullOrEmpty(designator) || airlines == null)
                return "Неизвестная авиакомпания";

            var code = designator.Split(' ')[0];
            return airlines.ContainsKey(code) ? airlines[code].Name : "Неизвестная авиакомпания";
        }

        private string GetCityName(Dictionary<string, Airport> airports, string airportCode)
        {
            return airports != null && airports.ContainsKey(airportCode)
                ? airports[airportCode].City
                : airportCode ?? "Неизвестный город";
        }

        private string GenerateSignature(string token, string marker, object request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var data = $"{token}:{marker}:{json}";
                using var md5 = MD5.Create();
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации подписи");
                return "invalid_signature";
            }
        }

        private string MapClassToTripClass(string classType)
        {
            return classType?.ToLower() switch
            {
                "business" => "C",
                "first" => "F",
                _ => "Y"
            };
        }

        private string MapTripClassToClass(string tripClass)
        {
            return tripClass?.ToUpper() switch
            {
                "C" => "business",
                "F" => "first",
                _ => "economy"
            };
        }

        private string ExtractIataCode(string cityString)
        {
            if (string.IsNullOrEmpty(cityString)) return cityString;

            // Если строка содержит код в скобках - извлекаем его
            var match = System.Text.RegularExpressions.Regex.Match(cityString, @"\(([A-Z]{3})\)");
            return match.Success ? match.Groups[1].Value : cityString;
        }
    }

    // Вспомогательный класс для автодополнения городов
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