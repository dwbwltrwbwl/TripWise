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

                var firstResponse = await MakeFirstRequest(rzdRequest);

                if (firstResponse.Result == "RID" && !string.IsNullOrEmpty(firstResponse.Rid))
                {
                    // Второй запрос с полученным RID
                    var trains = await MakeSecondRequest(firstResponse.Rid);
                    return MapToTrainResponse(trains, request);
                }
                else
                {
                    _logger.LogWarning("Не удалось получить RID от RZD API");
                    return new List<TrainSearchResponse>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске поездов через RZD API");
                throw;
            }
        }

        private async Task<RzdApiResponse> MakeFirstRequest(RzdApiRequest request)
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

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RzdApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private async Task<List<RzdRoute>> MakeSecondRequest(string rid)
        {
            var url = $"https://pass.rzd.ru/timetable/public/ru?layer_id=5827&rid={rid}";

            // Ждем немного перед вторым запросом (как в оригинальном API)
            await Task.Delay(1000);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<RzdApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return apiResponse?.Lst ?? new List<RzdRoute>();
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
                    Type = MapCarType(car.TypeLoc),
                    Price = car.Tariff
                }).ToList() ?? new List<TrainCategory>()
            }).ToList();
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
}
