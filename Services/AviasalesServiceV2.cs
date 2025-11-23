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

        // Добавьте эти методы если они нужны
        Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request);
        Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0);
        Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId);
    }

    public class AviasalesServiceV2 : AviasalesServiceV2
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AviasalesRealService> _logger;

        public AviasalesRealService(HttpClient httpClient, IConfiguration configuration, ILogger<AviasalesRealService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                var marker = _configuration["TravelPayouts:Marker"];
                var token = _configuration["TravelPayouts:Token"];
                var baseUrl = _configuration["TravelPayouts:ApiBaseUrl"];

                // Создаем запрос для Aviasales API
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

                // Генерируем подпись
                var signature = GenerateSignature(token, marker, searchRequest);
                var userIp = "127.0.0.1"; // В продакшене получайте реальный IP пользователя
                var host = "yourdomain.com"; // Ваш домен

                // Отправляем запрос на старт поиска
                var startRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/search/affiliate/start")
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

                // Ждем немного для сбора результатов
                await Task.Delay(10000);

                // Получаем результаты поиска
                var results = await GetSearchResultsAsync(searchResponse.SearchId, searchResponse.ResultsUrl);
                return ConvertToFlights(results);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске авиабилетов через Aviasales API");
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

        private async Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl)
        {
            var resultsRequest = new
            {
                search_id = searchId,
                limit = 50,
                last_update_timestamp = 0L
            };

            var token = _configuration["TravelPayouts:Token"];
            var url = $"{resultsUrl}/search/affiliate/results";

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(resultsRequest), Encoding.UTF8, "application/json")
            };

            requestMessage.Headers.Add("x-affiliate-user-id", token);

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AviasalesResultsResponse>(json);
        }

        private List<Flight> ConvertToFlights(AviasalesResultsResponse response)
        {
            var flights = new List<Flight>();

            if (response?.Tickets == null) return flights;

            foreach (var ticket in response.Tickets.Take(10)) // Ограничиваем количество
            {
                var cheapestProposal = ticket.Proposals?.OrderBy(p => p.Price?.Amount ?? decimal.MaxValue).FirstOrDefault();
                if (cheapestProposal == null) continue;

                var firstSegment = ticket.Segments?.FirstOrDefault();
                if (firstSegment?.Flights == null || !firstSegment.Flights.Any()) continue;

                var firstFlightIndex = firstSegment.Flights.First();
                if (firstFlightIndex >= response.FlightLegs.Count) continue;

                var flightLeg = response.FlightLegs[firstFlightIndex];

                var flight = new Flight
                {
                    Id = ticket.Id,
                    Airline = GetAirlineName(response.Airlines, flightLeg.OperatingCarrierDesignator),
                    FlightNumber = flightLeg.OperatingCarrierDesignator,
                    DepartureCity = GetCityName(response.Airports, flightLeg.Origin),
                    ArrivalCity = GetCityName(response.Airports, flightLeg.Destination),
                    DepartureAirport = flightLeg.Origin,
                    ArrivalAirport = flightLeg.Destination,
                    DepartureTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.DepartureUnixTimestamp).DateTime,
                    ArrivalTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.ArrivalUnixTimestamp).DateTime,
                    Price = cheapestProposal.Price?.Amount ?? 0,
                    Duration = (int)(flightLeg.ArrivalUnixTimestamp - flightLeg.DepartureUnixTimestamp) / 60,
                    Transfers = ticket.Segments?.Sum(s => s.Transfers?.Count ?? 0) ?? 0,
                    Currency = cheapestProposal.Price?.Currency ?? "RUB"
                };

                flights.Add(flight);
            }

            return flights.OrderBy(f => f.Price).ToList();
        }

        private string GetAirlineName(Dictionary<string, Airline> airlines, string designator)
        {
            var code = designator?.Split(' ')[0];
            return code != null && airlines.ContainsKey(code) ? airlines[code].Name : "Неизвестная авиакомпания";
        }

        private string GetCityName(Dictionary<string, Airport> airports, string airportCode)
        {
            return airports.ContainsKey(airportCode) ? airports[airportCode].City : airportCode;
        }

        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            // Используем API для поиска городов
            try
            {
                var url = $"https://autocomplete.travelpayouts.com/places2?term={Uri.EscapeDataString(query)}&locale=ru&types[]=airport&types[]=city";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var citiesData = JsonSerializer.Deserialize<List<dynamic>>(json);

                    return citiesData.Select(c => new City
                    {
                        Code = c.GetProperty("code").GetString(),
                        Name = c.GetProperty("name").GetString(),
                        Country = c.GetProperty("country_name").GetString(),
                        Type = c.GetProperty("type").GetString(),
                        Airport = c.GetProperty("type").GetString() == "airport" ? c.GetProperty("name").GetString() : ""
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов");
            }

            return new List<City>();
        }

        private string GenerateSignature(string token, string marker, object request)
        {
            // Упрощенная генерация подписи (нужно реализовать по документации Aviasales)
            var parameters = $"{token}:{marker}";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(parameters));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
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

        private string ExtractIataCode(string cityString)
        {
            var match = System.Text.RegularExpressions.Regex.Match(cityString, @"\(([A-Z]{3})\)");
            return match.Success ? match.Groups[1].Value : cityString;
        }
    }
}