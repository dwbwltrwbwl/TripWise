using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        // Добавляем метод конвертации
        List<Flight> ConvertToFlights(AviasalesResultsResponse response);
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
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
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
                    throw new Exception("Не настроены учетные данные TravelPayouts");
                }

                var searchRequest = new
                {
                    marker = marker,
                    locale = "ru",
                    currency_code = "RUB",
                    market_code = "ru",
                    search_params = new
                    {
                        trip_class = MapClassToTripClass(request.Class),
                        passengers = new
                        {
                            adults = request.Passengers,
                            children = 0,
                            infants = 0
                        },
                        directions = CreateDirections(request)
                    }
                };

                var signature = GenerateSignature(token, marker, searchRequest);
                var userIp = HttpContextHelper.GetUserIp(); // Нужно реализовать получение IP
                var host = "yourdomain.com";

                var startRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/prices/search/affiliate/start")
                {
                    Content = new StringContent(JsonSerializer.Serialize(searchRequest), Encoding.UTF8, "application/json")
                };

                startRequest.Headers.Add("x-real-host", host);
                startRequest.Headers.Add("x-user-ip", userIp);
                startRequest.Headers.Add("x-signature", signature);
                startRequest.Headers.Add("x-affiliate-user-id", token);

                var startResponse = await _httpClient.SendAsync(startRequest);
                startResponse.EnsureSuccessStatusCode();

                var startContent = await startResponse.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<AviasalesSearchResponseV2>(startContent);

                if (string.IsNullOrEmpty(searchResponse?.SearchId))
                {
                    throw new Exception("Не удалось получить search_id");
                }

                return searchResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при старте поиска авиабилетов");
                throw;
            }
        }

        public async Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                // Запускаем поиск
                var searchResponse = await StartSearchAsync(request);

                // Ждем некоторое время для сбора результатов
                await Task.Delay(5000);

                // Получаем результаты
                var results = await GetSearchResultsAsync(searchResponse.SearchId, searchResponse.ResultsUrl);

                // Конвертируем в нашу модель
                return ConvertToFlights(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске авиабилетов через Aviasales API");
                throw;
            }
        }

        public async Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0)
        {
            try
            {
                var token = _configuration["TravelPayouts:Token"];
                var resultsRequest = new
                {
                    search_id = searchId,
                    limit = 50,
                    last_update_timestamp = lastUpdateTimestamp
                };

                // Используем полный URL из resultsUrl
                var url = $"{resultsUrl}?search_id={searchId}";

                var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                requestMessage.Headers.Add("x-affiliate-user-id", token);

                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AviasalesResultsResponse>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении результатов поиска");
                throw;
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
                var userIp = HttpContextHelper.GetUserIp();
                var host = "yourdomain.com";

                var url = $"{resultsUrl}/v2/prices/search/affiliate/click";

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(clickRequest), Encoding.UTF8, "application/json")
                };

                requestMessage.Headers.Add("x-real-host", host);
                requestMessage.Headers.Add("x-user-ip", userIp);
                requestMessage.Headers.Add("x-signature", signature);
                requestMessage.Headers.Add("x-affiliate-user-id", token);

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

        private List<object> CreateDirections(FlightSearchRequest request)
        {
            var directions = new List<object>
            {
                new
                {
                    origin = ExtractIataCode(request.DepartureCity),
                    destination = ExtractIataCode(request.ArrivalCity),
                    date = request.DepartureDate.ToString("yyyy-MM-dd")
                }
            };

            if (request.ReturnDate.HasValue && request.TripType == "round")
            {
                directions.Add(new
                {
                    origin = ExtractIataCode(request.ArrivalCity),
                    destination = ExtractIataCode(request.DepartureCity),
                    date = request.ReturnDate.Value.ToString("yyyy-MM-dd")
                });
            }

            return directions;
        }

        private List<Flight> ConvertToFlights(AviasalesResultsResponse response)
        {
            var flights = new List<Flight>();

            if (response?.Tickets == null)
            {
                _logger.LogWarning("Нет билетов в ответе");
                return flights;
            }

            foreach (var ticket in response.Tickets.Take(20))
            {
                try
                {
                    var cheapestProposal = ticket.Proposals?.OrderBy(p => p.Price?.Amount ?? decimal.MaxValue).FirstOrDefault();
                    if (cheapestProposal == null) continue;

                    // Обрабатываем сегменты перелета
                    var segments = new List<FlightSegment>();
                    int totalDuration = 0;
                    int transfersCount = 0;

                    if (ticket.Segments != null)
                    {
                        foreach (var segment in ticket.Segments)
                        {
                            if (segment.Flights != null)
                            {
                                foreach (var flightIndex in segment.Flights)
                                {
                                    if (flightIndex < response.FlightLegs.Count)
                                    {
                                        var flightLeg = response.FlightLegs[flightIndex];
                                        var segmentDuration = (int)(flightLeg.ArrivalUnixTimestamp - flightLeg.DepartureUnixTimestamp) / 60;
                                        totalDuration += segmentDuration;

                                        var flightSegment = new FlightSegment
                                        {
                                            DepartureAirport = flightLeg.Origin,
                                            ArrivalAirport = flightLeg.Destination,
                                            DepartureTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.DepartureUnixTimestamp).DateTime,
                                            ArrivalTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.ArrivalUnixTimestamp).DateTime,
                                            Airline = GetAirlineName(response.Airlines, flightLeg.OperatingCarrierDesignator),
                                            FlightNumber = flightLeg.OperatingCarrierDesignator,
                                            Duration = segmentDuration,
                                            Aircraft = flightLeg.Equipment?.Name
                                        };
                                        segments.Add(flightSegment);
                                    }
                                }
                            }

                            transfersCount += segment.Transfers?.Count ?? 0;
                        }
                    }

                    var firstSegment = segments.FirstOrDefault();
                    var lastSegment = segments.LastOrDefault();

                    if (firstSegment != null)
                    {
                        var flight = new Flight
                        {
                            Id = ticket.Id,
                            Airline = firstSegment.Airline,
                            FlightNumber = firstSegment.FlightNumber,
                            DepartureCity = GetCityName(response.Airports, firstSegment.DepartureAirport),
                            ArrivalCity = GetCityName(response.Airports, lastSegment?.ArrivalAirport ?? firstSegment.ArrivalAirport),
                            DepartureAirport = firstSegment.DepartureAirport,
                            ArrivalAirport = lastSegment?.ArrivalAirport ?? firstSegment.ArrivalAirport,
                            DepartureTime = firstSegment.DepartureTime,
                            ArrivalTime = lastSegment?.ArrivalTime ?? firstSegment.ArrivalTime,
                            Price = cheapestProposal.Price?.Amount ?? 0,
                            Currency = cheapestProposal.Price?.Currency ?? "RUB",
                            Transfers = transfersCount,
                            Duration = totalDuration,
                            Class = MapTripClassToClass(response.SearchParams?.TripClass ?? "Y")
                        };

                        flights.Add(flight);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при конвертации билета {TicketId}", ticket.Id);
                }
            }

            return flights.OrderBy(f => f.Price).ToList();
        }

        private string GetAirlineName(Dictionary<string, Airline> airlines, string designator)
        {
            if (string.IsNullOrEmpty(designator)) return "Неизвестная авиакомпания";

            var code = designator.Split(' ')[0];
            return code != null && airlines != null && airlines.ContainsKey(code)
                ? airlines[code].Name
                : "Неизвестная авиакомпания";
        }

        private string GetCityName(Dictionary<string, Airport> airports, string airportCode)
        {
            return airports != null && airports.ContainsKey(airportCode)
                ? airports[airportCode].City
                : airportCode;
        }

        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                var url = $"https://autocomplete.travelpayouts.com/places2?term={Uri.EscapeDataString(query)}&locale=ru&types[]=airport&types[]=city";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    var cities = new List<City>();

                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        try
                        {
                            var city = new City
                            {
                                Code = element.TryGetProperty("code", out var code) ? code.GetString() : "",
                                Name = element.TryGetProperty("name", out var name) ? name.GetString() : "",
                                Country = element.TryGetProperty("country_name", out var country) ? country.GetString() : "",
                                CountryCode = element.TryGetProperty("country_code", out var countryCode) ? countryCode.GetString() : "",
                                Type = element.TryGetProperty("type", out var type) ? type.GetString() : ""
                            };

                            if (city.Type == "airport")
                            {
                                city.Airport = city.Name;
                                city.Name = element.TryGetProperty("city_name", out var cityName) ? cityName.GetString() : city.Name;
                            }

                            if (!string.IsNullOrEmpty(city.Code) && !string.IsNullOrEmpty(city.Name))
                            {
                                cities.Add(city);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Ошибка при парсинге города");
                        }
                    }

                    return cities;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов для запроса: {Query}", query);
            }

            return new List<City>();
        }

        private string GenerateSignature(string token, string marker, object request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
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

    // Вспомогательный класс для получения IP (нужно реализовать в зависимости от вашего контекста)
    public static class HttpContextHelper
    {
        public static string GetUserIp()
        {
            // В реальном приложении здесь должен быть код для получения IP пользователя
            // Например, через IHttpContextAccessor
            return "127.0.0.1";
        }
    }
}