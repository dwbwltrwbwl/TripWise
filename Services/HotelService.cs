using System.Text.Json;
using TripWise.Models;
using Microsoft.Extensions.Caching.Memory;

namespace TripWise.Services
{
    public class HotelService : IHotelService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<HotelService> _logger;

        public HotelService(IHttpClientFactory httpClientFactory,
                           IMemoryCache memoryCache,
                           ILogger<HotelService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _memoryCache = memoryCache;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TripWise/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request)
        {
            try
            {
                // Получаем координаты города, если указан только город
                if (!string.IsNullOrWhiteSpace(request.City) &&
                    !request.Latitude.HasValue && !request.Longitude.HasValue)
                {
                    var coords = await GetCityCoordinatesAsync(request.City);
                    if (coords == null)
                    {
                        return new List<Hotel>();
                    }

                    request.Latitude = coords.Latitude;
                    request.Longitude = coords.Longitude;
                }

                // Проверяем координаты
                if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                {
                    return new List<Hotel>();
                }

                // Ищем отели в OSM
                return await SearchOSMHotelsAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в HotelService.SearchHotelsAsync");
                return new List<Hotel>();
            }
        }

        private async Task<CityCoordinates> GetCityCoordinatesAsync(string city)
        {
            try
            {
                var cacheKey = $"city_coords_{city}";

                if (_memoryCache.TryGetValue(cacheKey, out CityCoordinates cachedCoords))
                {
                    return cachedCoords;
                }

                var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(city)}&limit=1&accept-language=ru";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);

                if (results == null || results.Count == 0)
                {
                    return null;
                }

                var result = results[0];
                var coords = new CityCoordinates
                {
                    Latitude = double.Parse(result.Lat, System.Globalization.CultureInfo.InvariantCulture),
                    Longitude = double.Parse(result.Lon, System.Globalization.CultureInfo.InvariantCulture),
                    DisplayName = result.Display_Name
                };

                // Кэшируем на 24 часа
                _memoryCache.Set(cacheKey, coords, TimeSpan.FromHours(24));

                return coords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при геокодировании города: {City}", city);
                return null;
            }
        }

        private async Task<List<Hotel>> SearchOSMHotelsAsync(HotelSearchRequest request)
        {
            var cacheKey = $"hotels_{request.Latitude}_{request.Longitude}_{request.Radius}_{request.AccommodationType}";

            if (_memoryCache.TryGetValue(cacheKey, out List<Hotel> cachedHotels))
            {
                return cachedHotels;
            }

            // Формируем Overpass QL запрос
            var query = BuildOverpassQuery(request);

            var url = "https://overpass-api.de/api/interpreter";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("data", query)
            });

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var osmData = JsonSerializer.Deserialize<OverpassResponse>(json);

            var hotels = ProcessOSMResponse(osmData, request.Latitude.Value, request.Longitude.Value, request.MinStars);

            // Кэшируем на 1 час
            _memoryCache.Set(cacheKey, hotels, TimeSpan.FromHours(1));

            return hotels;
        }

        private string BuildOverpassQuery(HotelSearchRequest request)
        {
            var radius = request.Radius;
            var lat = request.Latitude.Value;
            var lon = request.Longitude.Value;
            var type = request.AccommodationType;

            string tourismFilters;

            if (type == "all")
            {
                tourismFilters = @"
                    node[""tourism""=""hotel""](around:{radius},{lat},{lon});
                    node[""tourism""=""hostel""](around:{radius},{lat},{lon});
                    node[""tourism""=""guest_house""](around:{radius},{lat},{lon});
                    node[""tourism""=""apartment""](around:{radius},{lat},{lon});
                    node[""tourism""=""motel""](around:{radius},{lat},{lon});
                    node[""tourism""=""camp_site""](around:{radius},{lat},{lon});
                    node[""building""=""hotel""](around:{radius},{lat},{lon});
                ";
            }
            else
            {
                tourismFilters = $"node[\"tourism\"=\"{type}\"](around:{radius},{lat},{lon});";
            }

            return $@"
                [out:json][timeout:30];
                (
                    {tourismFilters}
                );
                out body;
                >;
                out skel qt;
            ".Replace("{radius}", radius.ToString())
             .Replace("{lat}", lat.ToString(System.Globalization.CultureInfo.InvariantCulture))
             .Replace("{lon}", lon.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private List<Hotel> ProcessOSMResponse(OverpassResponse osmData, double centerLat, double centerLon, int? minStars)
        {
            var hotels = new List<Hotel>();

            if (osmData?.Elements == null)
            {
                return hotels;
            }

            foreach (var element in osmData.Elements)
            {
                if (element.Tags == null || string.IsNullOrEmpty(element.Tags.GetValueOrDefault("name")))
                {
                    continue;
                }

                var hotel = new Hotel
                {
                    Id = element.Id.ToString(),
                    Name = element.Tags["name"],
                    Latitude = element.Lat,
                    Longitude = element.Lon,
                    Tags = element.Tags,
                    OSMUrl = $"https://www.openstreetmap.org/node/{element.Id}",
                    Distance = CalculateDistance(centerLat, centerLon, element.Lat, element.Lon)
                };

                // Извлекаем адрес
                hotel.Address = BuildAddress(element.Tags);

                // Извлекаем телефон и сайт
                hotel.Phone = element.Tags.GetValueOrDefault("phone")
                           ?? element.Tags.GetValueOrDefault("contact:phone");
                hotel.Website = element.Tags.GetValueOrDefault("website")
                              ?? element.Tags.GetValueOrDefault("contact:website");

                // Определяем тип жилья
                hotel.AccommodationType = GetAccommodationType(element.Tags);

                // Определяем количество звезд
                if (int.TryParse(element.Tags.GetValueOrDefault("stars"), out int stars))
                {
                    hotel.Stars = stars;
                }

                hotels.Add(hotel);
            }

            // Сортируем по расстоянию
            hotels = hotels.OrderBy(h => h.Distance).ToList();

            // Фильтруем по минимальному количеству звезд
            if (minStars.HasValue)
            {
                hotels = hotels.Where(h => h.Stars >= minStars.Value).ToList();
            }

            return hotels;
        }

        private string BuildAddress(Dictionary<string, string> tags)
        {
            var addressParts = new List<string>();

            if (tags.TryGetValue("addr:street", out var street))
            {
                if (tags.TryGetValue("addr:housenumber", out var houseNumber))
                {
                    addressParts.Add($"{street} {houseNumber}");
                }
                else
                {
                    addressParts.Add(street);
                }
            }

            if (tags.TryGetValue("addr:city", out var city))
            {
                addressParts.Add(city);
            }

            return addressParts.Count > 0 ? string.Join(", ", addressParts) : "Адрес не указан";
        }

        private string GetAccommodationType(Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("tourism", out var tourismType))
            {
                return tourismType switch
                {
                    "hotel" => "Отель",
                    "hostel" => "Хостел",
                    "guest_house" => "Гостевой дом",
                    "apartment" => "Апартаменты",
                    "motel" => "Мотель",
                    "camp_site" => "Кемпинг",
                    _ => "Другое"
                };
            }

            if (tags.TryGetValue("building", out var buildingType) && buildingType == "hotel")
            {
                return "Отель";
            }

            return "Другое";
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Радиус Земли в метрах

            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}