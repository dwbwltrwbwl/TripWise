using System.Text.Json;
using System.Web;
using TripWise.Models;

public class RzdApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RzdApiService> _logger;

    public RzdApiService(HttpClient httpClient, ILogger<RzdApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Настраиваем HttpClient для имитации браузера
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://ticket.rzd.ru/");
    }

    public async Task<List<TrainSearchResponse>> SearchTrains(TrainSearchRequest request)
    {
        try
        {
            _logger.LogInformation($"Начало поиска поездов: {request.DepartureStationId} -> {request.ArrivalStationId} на {request.DepartureDate}");

            // Первый запрос для получения RID
            var rzdRequest = new RzdApiRequest
            {
                Code0 = request.DepartureStationId,
                Code1 = request.ArrivalStationId,
                Dt0 = FormatDateForRzd(request.DepartureDate),
                Dir = 0,
                Tfl = 3,
                CheckSeats = 1
            };

            _logger.LogInformation($"Первый запрос к RZD API: {rzdRequest.Code0} -> {rzdRequest.Code1} на {rzdRequest.Dt0}");

            var firstResponse = await MakeFirstRequest(rzdRequest);

            if (firstResponse?.Result == "RID" && !string.IsNullOrEmpty(firstResponse.Rid))
            {
                _logger.LogInformation($"Получен RID: {firstResponse.Rid}");

                // Второй запрос с полученным RID
                var trains = await MakeSecondRequest(firstResponse.Rid);
                _logger.LogInformation($"Найдено поездов: {trains?.Count ?? 0}");

                return MapToTrainResponse(trains, request);
            }
            else
            {
                _logger.LogWarning($"Не удалось получить RID от RZD API. Result: {firstResponse?.Result}, RID: {firstResponse?.Rid}");
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
                ["dir"] = request.Dir.ToString(),
                ["tfl"] = request.Tfl.ToString(),
                ["checkSeats"] = request.CheckSeats.ToString(),
                ["code0"] = request.Code0,
                ["code1"] = request.Code1,
                ["dt0"] = request.Dt0
            };

            var queryString = string.Join("&", parameters.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}"));
            var url = $"https://pass.rzd.ru/timetable/public/ru?{queryString}";

            _logger.LogInformation($"URL первого запроса: {url}");

            var response = await _httpClient.GetAsync(url);
            _logger.LogInformation($"Статус ответа первого запроса: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Ошибка HTTP: {response.StatusCode}");
                return new RzdApiResponse { Result = "ERROR" };
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Ответ первого запроса: {content}");

            var apiResponse = JsonSerializer.Deserialize<RzdApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return apiResponse ?? new RzdApiResponse { Result = "ERROR" };
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
            _logger.LogInformation($"URL второго запроса: {url}");

            // Ждем немного перед вторым запросом (как в оригинальном API)
            await Task.Delay(2000);

            var response = await _httpClient.GetAsync(url);
            _logger.LogInformation($"Статус ответа второго запроса: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Ошибка HTTP второго запроса: {response.StatusCode}");
                return new List<RzdRoute>();
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Ответ второго запроса: {content}");

            var apiResponse = JsonSerializer.Deserialize<RzdApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return apiResponse?.Lst ?? new List<RzdRoute>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка во втором запросе к RZD API");
            return new List<RzdRoute>();
        }
    }

    private List<TrainSearchResponse> MapToTrainResponse(List<RzdRoute> routes, TrainSearchRequest request)
    {
        if (routes == null || routes.Count == 0)
        {
            _logger.LogInformation("Нет маршрутов для преобразования");
            return new List<TrainSearchResponse>();
        }

        var result = routes.Select(route => new TrainSearchResponse
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
                Type = MapCarType(car.TypeLoc),
                Price = car.Tariff
            }).ToList() ?? new List<TrainCategory>()
        }).ToList();

        _logger.LogInformation($"Преобразовано {result.Count} поездов");
        return result;
    }

    private string MapCarType(string typeLoc)
    {
        if (string.IsNullOrEmpty(typeLoc)) return "other";

        return typeLoc.ToLower() switch
        {
            var t when t.Contains("плацкарт") => "plazcard",
            var t when t.Contains("купе") => "coupe",
            var t when t.Contains("сидячий") => "sedentary",
            var t when t.Contains("люкс") => "lux",
            var t when t.Contains("мягкий") => "soft",
            _ => "other"
        };
    }

    private string FormatDateForRzd(string date)
    {
        if (DateTime.TryParse(date, out DateTime dt))
        {
            return dt.ToString("dd.MM.yyyy");
        }
        return DateTime.Now.AddDays(1).ToString("dd.MM.yyyy");
    }
}