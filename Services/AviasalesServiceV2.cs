using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TripWise.Models;

namespace TripWise.Services
{
    public interface IAviasalesServiceV2
    {
        Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request);
        Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0);
        Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId);
        Task<List<SimplifiedFlight>> SearchFlightsAsync(FlightSearchRequest request);
        string GenerateSignature(string token, string marker, AviasalesSearchRequestV2 request);
    }

    public class AviasalesServiceV2 : IAviasalesServiceV2
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AviasalesServiceV2> _logger;

        public AviasalesServiceV2(HttpClient httpClient, IConfiguration configuration, ILogger<AviasalesServiceV2> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request)
        {
            try
            {
                var marker = _configuration["TravelPayouts:Marker"];
                var token = _configuration["TravelPayouts:Token"];
                var baseUrl = _configuration["TravelPayouts:ApiBaseUrl"];

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
                        Directions = new List<Direction>
                        {
                            new Direction
                            {
                                Origin = ExtractIataCode(request.DepartureCity),
                                Destination = ExtractIataCode(request.ArrivalCity),
                                Date = request.DepartureDate.ToString("yyyy-MM-dd")
                            }
                        }
                    }
                };

                // Добавляем обратный рейс если нужен
                if (request.ReturnDate.HasValue && request.TripType == "round")
                {
                    searchRequest.SearchParams.Directions.Add(new Direction
                    {
                        Origin = ExtractIataCode(request.ArrivalCity),
                        Destination = ExtractIataCode(request.DepartureCity),
                        Date = request.ReturnDate.Value.ToString("yyyy-MM-dd")
                    });
                }

                var signature = GenerateSignature(token, marker, searchRequest);
                var userIp = "123.123.123.123"; // В реальном приложении получайте IP пользователя
                var host = "your-domain.com"; // Ваш домен

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/search/affiliate/start")
                {
                    Content = new StringContent(JsonSerializer.Serialize(searchRequest), Encoding.UTF8, "application/json")
                };

                requestMessage.Headers.Add("x-real-host", host);
                requestMessage.Headers.Add("x-user-ip", userIp);
                requestMessage.Headers.Add("x-signature", signature);
                requestMessage.Headers.Add("x-affiliate-user-id", token);

                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<AviasalesSearchResponseV2>(json);

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
                var resultsRequest = new AviasalesResultsRequest
                {
                    SearchId = searchId,
                    Limit = 50,
                    LastUpdateTimestamp = lastUpdateTimestamp
                };

                var token = _configuration["TravelPayouts:Token"];
                var url = $"{resultsUrl}/search/affiliate/results";

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(resultsRequest), Encoding.UTF8, "application/json")
                };

                requestMessage.Headers.Add("x-affiliate-user-id", token);

                var response = await _httpClient.SendAsync(requestMessage);

                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    return new AviasalesResultsResponse { IsOver = false };
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<AviasalesResultsResponse>(json);

                return results;
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
                var marker = _configuration["TravelPayouts:Marker"];
                var url = $"{resultsUrl}/searches/{searchId}/clicks/{proposalId}";

                var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                requestMessage.Headers.Add("x-affiliate-user-id", marker);

                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var clickResponse = JsonSerializer.Deserialize<ClickResponseV2>(json);

                return clickResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении ссылки на бронирование");
                throw;
            }
        }

        public async Task<List<SimplifiedFlight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                // Запускаем поиск
                var searchResponse = await StartSearchAsync(request);

                // Ждем немного для сбора результатов
                await Task.Delay(5000);

                var results = new List<SimplifiedFlight>();
                long lastUpdateTimestamp = 0;
                var attempts = 0;
                const int maxAttempts = 12; // 60 секунд максимум

                while (attempts < maxAttempts)
                {
                    var searchResults = await GetSearchResultsAsync(searchResponse.SearchId, searchResponse.ResultsUrl, lastUpdateTimestamp);

                    if (searchResults.IsOver)
                    {
                        // Конвертируем результаты в упрощенный формат
                        results = ConvertToSimplifiedFlights(searchResults);
                        break;
                    }

                    if (searchResults.LastUpdateTimestamp > 0)
                    {
                        lastUpdateTimestamp = searchResults.LastUpdateTimestamp;
                    }

                    await Task.Delay(5000); // Ждем 5 секунд перед следующим запросом
                    attempts++;
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске авиабилетов");
                return new List<SimplifiedFlight>();
            }
        }

        public string GenerateSignature(string token, string marker, AviasalesSearchRequestV2 request)
        {
            // Собираем все параметры в алфавитном порядке
            var parameters = new Dictionary<string, string>
            {
                ["currency_code"] = request.CurrencyCode,
                ["locale"] = request.Locale,
                ["marker"] = marker,
                ["market_code"] = request.MarketCode
            };

            // Добавляем параметры search_params
            if (request.SearchParams != null)
            {
                parameters["search_params[trip_class]"] = request.SearchParams.TripClass;

                if (request.SearchParams.Passengers != null)
                {
                    parameters["search_params[passengers][adults]"] = request.SearchParams.Passengers.Adults.ToString();
                    parameters["search_params[passengers][children]"] = request.SearchParams.Passengers.Children.ToString();
                    parameters["search_params[passengers][infants]"] = request.SearchParams.Passengers.Infants.ToString();
                }

                if (request.SearchParams.Directions != null)
                {
                    for (int i = 0; i < request.SearchParams.Directions.Count; i++)
                    {
                        parameters[$"search_params[directions][{i}][date]"] = request.SearchParams.Directions[i].Date;
                        parameters[$"search_params[directions][{i}][destination]"] = request.SearchParams.Directions[i].Destination;
                        parameters[$"search_params[directions][{i}][origin]"] = request.SearchParams.Directions[i].Origin;
                    }
                }
            }

            // Сортируем по ключу и объединяем
            var sortedParams = parameters.OrderBy(p => p.Key).Select(p => p.Value);
            var paramString = string.Join(":", sortedParams);

            // Добавляем токен в начало
            var signatureString = token + ":" + paramString;

            // Создаем MD5 хеш
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(signatureString));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private List<SimplifiedFlight> ConvertToSimplifiedFlights(AviasalesResultsResponse response)
        {
            var simplifiedFlights = new List<SimplifiedFlight>();

            if (response.Tickets == null) return simplifiedFlights;

            foreach (var ticket in response.Tickets)
            {
                var simplifiedFlight = new SimplifiedFlight
                {
                    Id = ticket.Id,
                    Signature = ticket.Signature,
                    Segments = new List<FlightSegment>(),
                    Proposals = new List<SimplifiedProposal>()
                };

                // Обрабатываем сегменты
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
                                    var segmentInfo = new FlightSegment
                                    {
                                        DepartureAirport = flightLeg.Origin,
                                        ArrivalAirport = flightLeg.Destination,
                                        DepartureTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.DepartureUnixTimestamp).DateTime,
                                        ArrivalTime = DateTimeOffset.FromUnixTimeSeconds(flightLeg.ArrivalUnixTimestamp).DateTime,
                                        FlightNumber = flightLeg.OperatingCarrierDesignator,
                                        Aircraft = flightLeg.Equipment?.Name,
                                        Duration = (int)(flightLeg.ArrivalUnixTimestamp - flightLeg.DepartureUnixTimestamp) / 60
                                    };

                                    // Получаем название авиакомпании
                                    var airlineCode = flightLeg.OperatingCarrierDesignator?.Split(' ')[0];
                                    if (airlineCode != null && response.Airlines.ContainsKey(airlineCode))
                                    {
                                        segmentInfo.Airline = response.Airlines[airlineCode].Name;
                                    }

                                    simplifiedFlight.Segments.Add(segmentInfo);
                                }
                            }
                        }
                    }

                    simplifiedFlight.TransfersCount = ticket.Segments.Sum(s => s.Transfers?.Count ?? 0);
                    simplifiedFlight.TotalDuration = (int)(simplifiedFlight.Segments.Last().ArrivalTime - simplifiedFlight.Segments.First().DepartureTime).TotalMinutes;
                }

                // Обрабатываем предложения
                if (ticket.Proposals != null)
                {
                    foreach (var proposal in ticket.Proposals)
                    {
                        var simplifiedProposal = new SimplifiedProposal
                        {
                            Id = proposal.Id,
                            AgentId = proposal.AgentId,
                            Price = proposal.Price,
                            FlightTerms = proposal.FlightTerms
                        };

                        // Получаем название агента
                        if (response.Agents != null && response.Agents.ContainsKey(proposal.AgentId))
                        {
                            simplifiedProposal.AgentName = response.Agents[proposal.AgentId].Label;
                        }

                        simplifiedFlight.Proposals.Add(simplifiedProposal);
                    }
                }

                simplifiedFlights.Add(simplifiedFlight);
            }

            return simplifiedFlights.OrderBy(f => f.MinPrice).ToList();
        }

        private string MapClassToTripClass(string classType)
        {
            return classType?.ToLower() switch
            {
                "business" => "C",
                "first" => "F",
                "comfort" => "W",
                _ => "Y"
            };
        }

        private string ExtractIataCode(string cityString)
        {
            // Извлекаем IATA код из строки типа "Москва (MOW)"
            var match = System.Text.RegularExpressions.Regex.Match(cityString, @"\(([A-Z]{3})\)");
            return match.Success ? match.Groups[1].Value : cityString;
        }
    }
}