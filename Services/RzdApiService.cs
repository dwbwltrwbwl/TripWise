using System.Text.Json;
using System.Web;
using TripWise.Models;

namespace TripWise.Services
{
    public class RzdApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RzdApiService> _logger;

        public RzdApiService(HttpClient httpClient, ILogger<RzdApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://rasp.rzd.ru/");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://rasp.rzd.ru");
        }

        public async Task<List<TrainSearchResponse>> SearchTrains(TrainSearchRequest request)
        {
            try
            {
                _logger.LogInformation($"Поиск поездов: {request.DepartureStationId} -> {request.ArrivalStationId}");

                var rzdRequest = new RzdApiRequest
                {
                    Code0 = request.DepartureStationId,
                    Code1 = request.ArrivalStationId,
                    Dt0 = FormatDateForRzd(request.DepartureDate),
                    Dir = 0,
                    Tfl = 3,
                    CheckSeats = 1
                };

                var firstResponse = await MakeFirstRequest(rzdRequest);

                // Используем GetRid() вместо прямого доступа к Rid
                if (firstResponse?.Result == "RID" && !string.IsNullOrEmpty(firstResponse.GetRid()))
                {
                    _logger.LogInformation($"Получен RID: {firstResponse.GetRid()}");

                    var trains = await MakeSecondRequest(firstResponse.GetRid());
                    _logger.LogInformation($"Найдено поездов: {trains?.Count ?? 0}");

                    return MapToTrainResponse(trains, request);
                }
                else
                {
                    _logger.LogWarning($"Не удалось получить RID. Result: {firstResponse?.Result}");
                    return new List<TrainSearchResponse>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске поездов через RZD API");
                throw new Exception($"Ошибка поиска: {ex.Message}");
            }
        }

        private async Task<RzdApiResponse> MakeFirstRequest(RzdApiRequest request)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["layer_id"] = "5827",
                    ["dir"] = "0", // как строка
                    ["tfl"] = "1", // только поезда (без электричек)
                    ["checkSeats"] = "0", // все поезда (не только с билетами)
                    ["code0"] = request.Code0,
                    ["code1"] = request.Code1,
                    ["dt0"] = request.Dt0,
                    ["md"] = "0" // без пересадок
                };

                var queryString = string.Join("&", parameters.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}"));
                var url = $"https://pass.rzd.ru/timetable/public/ru?{queryString}";

                _logger.LogInformation($"Запрос к RZD: {url}");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP ошибка: {response.StatusCode}");
                    return new RzdApiResponse { Result = "ERROR" };
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Ответ RZD: {content}");

                // Используем JsonElement для гибкого парсинга
                using var jsonDoc = JsonDocument.Parse(content);
                var json = jsonDoc.RootElement;

                // Ищем поля в нижнем регистре (как в ответе RZD)
                string result = null;
                string rid = null;
                string timestamp = null;

                foreach (var property in json.EnumerateObject())
                {
                    if (property.Name.Equals("result", StringComparison.OrdinalIgnoreCase))
                        result = property.Value.GetString();
                    else if (property.Name.Equals("rid", StringComparison.OrdinalIgnoreCase))
                    {
                        rid = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Number => property.Value.GetInt64().ToString(),
                            _ => null
                        };
                    }
                    else if (property.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
                        timestamp = property.Value.GetString();
                }

                _logger.LogInformation($"Распарсено: result={result}, rid={rid}");

                return new RzdApiResponse
                {
                    Result = result,
                    Rid = rid,
                    Timestamp = timestamp,
                    Lst = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в первом запросе к RZD API");
                return new RzdApiResponse { Result = "ERROR" };
            }
        }

        private async Task<List<RzdRoute>> MakeSecondRequest(string rid)
        {
            try
            {
                var url = $"https://pass.rzd.ru/timetable/public/ru?layer_id=5827&rid={rid}";
                _logger.LogInformation($"Второй запрос: {url}");

                await Task.Delay(2000);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP ошибка второго запроса: {response.StatusCode}");
                    return new List<RzdRoute>();
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Ответ второго запроса получен, длина: {content.Length}");

                // Используем ту же гибкую десериализацию
                using var jsonDoc = JsonDocument.Parse(content);
                var json = jsonDoc.RootElement;

                // Ищем список поездов в новом формате
                List<RzdRoute> trains = new List<RzdRoute>();

                // Пробуем разные пути к данным
                if (json.TryGetProperty("tp", out var tpProperty) && tpProperty.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tpItem in tpProperty.EnumerateArray())
                    {
                        if (tpItem.TryGetProperty("list", out var listProperty) && listProperty.ValueKind == JsonValueKind.Array)
                        {
                            trains = JsonSerializer.Deserialize<List<RzdRoute>>(listProperty.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }) ?? new List<RzdRoute>();
                            _logger.LogInformation($"Найдено поездов через tp[].list: {trains.Count}");
                            break;
                        }
                    }
                }

                // Если не нашли в tp[], пробуем старый формат
                if (trains.Count == 0 && json.TryGetProperty("lst", out var lstProperty) && lstProperty.ValueKind == JsonValueKind.Array)
                {
                    trains = JsonSerializer.Deserialize<List<RzdRoute>>(lstProperty.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<RzdRoute>();
                    _logger.LogInformation($"Найдено поездов через lst: {trains.Count}");
                }

                // Если все еще не нашли, ищем в корне
                if (trains.Count == 0)
                {
                    foreach (var property in json.EnumerateObject())
                    {
                        if (property.Name.Equals("list", StringComparison.OrdinalIgnoreCase) &&
                            property.Value.ValueKind == JsonValueKind.Array)
                        {
                            trains = JsonSerializer.Deserialize<List<RzdRoute>>(property.Value.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }) ?? new List<RzdRoute>();
                            _logger.LogInformation($"Найдено поездов через list: {trains.Count}");
                            break;
                        }
                    }
                }

                _logger.LogInformation($"Итоговое количество маршрутов: {trains.Count}");
                return trains;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка во втором запросе к RZD API");
                return new List<RzdRoute>();
            }
        }

        private List<TrainSearchResponse> MapToTrainResponse(List<RzdRoute> routes, TrainSearchRequest request)
        {
            return routes.Select(route => new TrainSearchResponse
            {
                Name = route.Brand ?? "Поезд",
                DepartureStation = request.DepartureStationId,
                ArrivalStation = request.ArrivalStationId,
                DepartureTime = route.Time0,
                ArrivalTime = route.Time1,
                TrainNumber = route.Number,
                TravelTime = route.TimeInWay,
                Firm = (route.Carrier?.Contains("Фирменный") == true),
                Categories = route.Cars?.Select(car => new TrainCategory
                {
                    Type = MapCarType(car.TypeLoc, car.IType),
                    Price = car.Tariff
                }).ToList() ?? new List<TrainCategory>()
            }).ToList();
        }

        private string MapCarType(string typeLoc, int iType)
        {
            if (string.IsNullOrEmpty(typeLoc))
            {
                // Fallback по iType если typeLoc пустой
                return iType switch
                {
                    1 => "plazcard",
                    3 => "sedentary",
                    4 => "coupe",
                    5 => "soft",
                    6 => "lux",
                    _ => "other"
                };
            }

            return typeLoc.ToLower() switch
            {
                var t when t.Contains("плацкарт") => "plazcard",
                var t when t.Contains("плац") => "plazcard",
                var t when t.Contains("купе") => "coupe",
                var t when t.Contains("сидяч") => "sedentary",
                var t when t.Contains("св") => "lux",
                var t when t.Contains("люкс") => "lux",
                var t when t.Contains("мягк") => "soft",
                var t when t.Contains("эконом") => "sedentary",
                _ => "other"
            };
        }

        private string FormatDateForRzd(string date)
        {
            if (DateTime.TryParse(date, out DateTime dt) && dt > DateTime.Now)
            {
                return dt.ToString("dd.MM.yyyy");
            }
            // Используем завтрашнюю дату если что-то не так
            return DateTime.Now.AddDays(1).ToString("dd.MM.yyyy");
        }
    }
}