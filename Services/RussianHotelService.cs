using System.Text.Json;
using TripWise.Models;

namespace TripWise.Services
{
    public class RussianHotelService : IHotelService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RussianHotelService> _logger;

        public RussianHotelService(HttpClient httpClient, ILogger<RussianHotelService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Поиск отелей через российское API: {@Request}", request);

                // Пока возвращаем тестовые данные для проверки работы
                var testHotels = GetTestHotels(request.City);
                return testHotels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске отелей");
                return new List<Hotel>();
            }
        }

        public async Task<List<City>> SearchHotelCitiesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<City>();

                // Пока возвращаем тестовые города для проверки работы
                var cities = GetRussianCities()
                    .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();

                return cities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов");
                return new List<City>();
            }
        }

        // Тестовые данные для проверки
        private List<Hotel> GetTestHotels(string city)
        {
            return new List<Hotel>
            {
                new Hotel
                {
                    Id = "1",
                    Name = $"Shelter-Hotels {city}",
                    Address = $"{city}, центральный район",
                    Price = 4500,
                    Rating = 4.2m,
                    Stars = 4,
                    Description = "Комфортабельный отель в центре города",
                    Photos = new List<string> { "https://cdn1.ozonusercontent.com/s3/hotels-media-05/c1200/AZX2ssQqe5aKwxTXJLplXA.jpg" },
                    Amenities = new List<string> { "Wi-Fi", "Завтрак", "Парковка" },
                    Location = new Location { City = city, Country = "Россия" }
                },
                new Hotel
                {
                    Id = "2",
                    Name = $"Sadovaya loft {city}",
                    Address = $"{city}, исторический центр",
                    Price = 3800,
                    Rating = 4.0m,
                    Stars = 3,
                    Description = "Стильный лофт в историческом здании",
                    Photos = new List<string> { "https://avatars.mds.yandex.net/get-altay/14092818/2a0000019407c6988565f903de1ded3b0089/orig" },
                    Amenities = new List<string> { "Wi-Fi", "Кухня", "Стиральная машина" },
                    Location = new Location { City = city, Country = "Россия" }
                }
            };
        }

        private List<City> GetRussianCities()
        {
            return new List<City>
            {
                new City { Code = "1", Name = "Москва", Country = "Россия", Type = "city" },
                new City { Code = "2", Name = "Санкт-Петербург", Country = "Россия", Type = "city" },
                new City { Code = "3", Name = "Сочи", Country = "Россия", Type = "city" },
                new City { Code = "4", Name = "Казань", Country = "Россия", Type = "city" },
                new City { Code = "5", Name = "Екатеринбург", Country = "Россия", Type = "city" },
                new City { Code = "6", Name = "Новосибирск", Country = "Россия", Type = "city" },
                new City { Code = "7", Name = "Краснодар", Country = "Россия", Type = "city" },
                new City { Code = "8", Name = "Калининград", Country = "Россия", Type = "city" },
                new City { Code = "9", Name = "Владивосток", Country = "Россия", Type = "city" },
                new City { Code = "10", Name = "Ростов-на-Дону", Country = "Россия", Type = "city" }
            };
        }
    }
}