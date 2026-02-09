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
                _logger.LogInformation($"Дата: {request.DepartureDate}, IsReturn: {request.IsReturn}");

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
                    ["dir"] = "0",
                    ["tfl"] = "1",
                    ["checkSeats"] = "0",
                    ["code0"] = request.Code0,
                    ["code1"] = request.Code1,
                    ["dt0"] = request.Dt0,
                    ["md"] = "0"
                };

                var queryString = string.Join("&", parameters.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}"));
                var url = $"https://pass.rzd.ru/timetable/public/ru?{queryString}";

                _logger.LogDebug($"Запрос к RZD: {url}");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP ошибка: {response.StatusCode}");
                    return new RzdApiResponse { Result = "ERROR" };
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Ответ RZD получен, длина: {content.Length}");

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

                _logger.LogDebug($"Распарсено: result={result}, rid={rid}");

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
                _logger.LogDebug($"Второй запрос: {url}");

                await Task.Delay(2000);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP ошибка второго запроса: {response.StatusCode}");
                    return new List<RzdRoute>();
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Ответ второго запроса получен, длина: {content.Length}");

                // Используем гибкую десериализацию
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
                            _logger.LogDebug($"Найдено поездов через tp[].list: {trains.Count}");
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
                    _logger.LogDebug($"Найдено поездов через lst: {trains.Count}");
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
                            _logger.LogDebug($"Найдено поездов через list: {trains.Count}");
                            break;
                        }
                    }
                }

                _logger.LogDebug($"Итоговое количество маршрутов: {trains.Count}");
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
            var responses = new List<TrainSearchResponse>();

            foreach (var route in routes)
            {
                try
                {
                    var response = new TrainSearchResponse
                    {
                        Name = route.Brand ?? "Поезд",
                        DepartureStation = request.DepartureStationId,
                        ArrivalStation = request.ArrivalStationId,
                        DepartureTime = route.Time0 ?? "00:00",
                        ArrivalTime = route.Time1 ?? "00:00",
                        TrainNumber = route.Number ?? "0000",
                        TravelTime = route.TimeInWay ?? "00:00",
                        Firm = (route.Carrier?.Contains("Фирменный") == true) || (route.BFirm),
                        IsReturn = request.IsReturn,
                        Categories = new List<TrainCategory>()
                    };

                    // Обрабатываем категории вагонов
                    if (route.Cars != null && route.Cars.Any())
                    {
                        foreach (var car in route.Cars)
                        {
                            var category = new TrainCategory
                            {
                                Type = MapCarType(car.TypeLoc, car.IType),
                                Price = car.Tariff > 0 ? car.Tariff : GetDefaultPrice(car.TypeLoc, car.IType)
                            };
                            response.Categories.Add(category);
                        }
                    }
                    else
                    {
                        // Если нет данных о вагонах, добавляем стандартные категории
                        response.Categories.AddRange(GetDefaultCategories());
                    }

                    responses.Add(response);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка маппинга маршрута");
                }
            }

            return responses;
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

            var lowerType = typeLoc.ToLower();

            if (lowerType.Contains("плацкарт") || lowerType.Contains("плац"))
                return "plazcard";
            if (lowerType.Contains("купе"))
                return "coupe";
            if (lowerType.Contains("сидяч"))
                return "sedentary";
            if (lowerType.Contains("св") || lowerType.Contains("люкс"))
                return "lux";
            if (lowerType.Contains("мягк"))
                return "soft";
            if (lowerType.Contains("эконом"))
                return "sedentary";

            return "other";
        }

        private decimal GetDefaultPrice(string typeLoc, int iType)
        {
            return MapCarType(typeLoc, iType) switch
            {
                "plazcard" => 1500,
                "coupe" => 3000,
                "sedentary" => 1000,
                "lux" => 5000,
                "soft" => 4000,
                _ => 2000
            };
        }

        private List<TrainCategory> GetDefaultCategories()
        {
            return new List<TrainCategory>
            {
                new TrainCategory { Type = "plazcard", Price = 1500 },
                new TrainCategory { Type = "coupe", Price = 3000 },
                new TrainCategory { Type = "lux", Price = 5000 }
            };
        }

        private string FormatDateForRzd(string date)
        {
            if (DateTime.TryParse(date, out DateTime dt))
            {
                return dt.ToString("dd.MM.yyyy");
            }

            // Используем завтрашнюю дату если что-то не так
            return DateTime.Now.AddDays(1).ToString("dd.MM.yyyy");
        }
    }

    // Внутренние модели для работы с RZD API
    public class RzdApiRequest
    {
        public string Code0 { get; set; }
        public string Code1 { get; set; }
        public string Dt0 { get; set; }
        public int Dir { get; set; } = 0;
        public int Tfl { get; set; } = 3;
        public int CheckSeats { get; set; } = 1;
    }

    public class RzdApiResponse
    {
        public string Result { get; set; }
        public string Rid { get; set; }
        public long? RID { get; set; }
        public string Timestamp { get; set; }
        public List<RzdRoute> Lst { get; set; }

        public string GetRid() => Rid ?? RID?.ToString();
    }

    public class RzdRoute
    {
        public string Number { get; set; }
        public string Number2 { get; set; }
        public string Brand { get; set; }
        public string Carrier { get; set; }
        public string Route0 { get; set; }
        public string Route1 { get; set; }
        public string Station0 { get; set; }
        public string Station1 { get; set; }
        public string Date0 { get; set; }
        public string Time0 { get; set; }
        public string Date1 { get; set; }
        public string Time1 { get; set; }
        public string TimeInWay { get; set; }
        public bool BFirm { get; set; }
        public List<RzdCar> Cars { get; set; }
    }

    public class RzdCar
    {
        public string Type { get; set; }
        public string TypeLoc { get; set; }
        public string ServCls { get; set; }
        public int FreeSeats { get; set; }
        public decimal Tariff { get; set; }
        public int IType { get; set; }
    }
}