using TripWise.Models;

namespace TripWise.Services
{
    public class SimpleAviasalesService : IAviasalesRealService
    {
        private readonly ILogger<SimpleAviasalesService> _logger;

        public SimpleAviasalesService(ILogger<SimpleAviasalesService> logger)
        {
            _logger = logger;
        }

        public async Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Используется упрощенный сервис поиска для маршрута: {Departure} -> {Arrival}",
                    request.DepartureCity, request.ArrivalCity);

                // Имитируем небольшую задержку как у реального API
                await Task.Delay(500);

                // Возвращаем демо-данные для тестирования
                var flights = new List<Flight>
                {
                    new Flight
                    {
                        Id = "1",
                        Airline = "Аэрофлот",
                        FlightNumber = "SU-100",
                        DepartureCity = request.DepartureCity,
                        ArrivalCity = request.ArrivalCity,
                        DepartureAirport = "SVO",
                        ArrivalAirport = "LED",
                        DepartureTime = request.DepartureDate.AddHours(10),
                        ArrivalTime = request.DepartureDate.AddHours(12),
                        Price = 4500,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 120,
                        Class = "economy",
                        IsReturn = false
                    },
                    new Flight
                    {
                        Id = "2",
                        Airline = "S7 Airlines",
                        FlightNumber = "S7-200",
                        DepartureCity = request.DepartureCity,
                        ArrivalCity = request.ArrivalCity,
                        DepartureAirport = "DME",
                        ArrivalAirport = "LED",
                        DepartureTime = request.DepartureDate.AddHours(14),
                        ArrivalTime = request.DepartureDate.AddHours(16),
                        Price = 5200,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 120,
                        Class = "economy",
                        IsReturn = false
                    },
                    new Flight
                    {
                        Id = "3",
                        Airline = "Победа",
                        FlightNumber = "DP-300",
                        DepartureCity = request.DepartureCity,
                        ArrivalCity = request.ArrivalCity,
                        DepartureAirport = "VKO",
                        ArrivalAirport = "LED",
                        DepartureTime = request.DepartureDate.AddHours(8),
                        ArrivalTime = request.DepartureDate.AddHours(10),
                        Price = 3200,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 120,
                        Class = "economy",
                        IsReturn = false
                    },
                    new Flight
                    {
                        Id = "4",
                        Airline = "Ural Airlines",
                        FlightNumber = "U6-400",
                        DepartureCity = request.DepartureCity,
                        ArrivalCity = request.ArrivalCity,
                        DepartureAirport = "SVO",
                        ArrivalAirport = "LED",
                        DepartureTime = request.DepartureDate.AddHours(18),
                        ArrivalTime = request.DepartureDate.AddHours(20).AddMinutes(30),
                        Price = 4800,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 150,
                        Class = "economy",
                        IsReturn = false
                    },
                    new Flight
                    {
                        Id = "5",
                        Airline = "Nordwind Airlines",
                        FlightNumber = "N4-500",
                        DepartureCity = request.DepartureCity,
                        ArrivalCity = request.ArrivalCity,
                        DepartureAirport = "DME",
                        ArrivalAirport = "LED",
                        DepartureTime = request.DepartureDate.AddHours(6),
                        ArrivalTime = request.DepartureDate.AddHours(8).AddMinutes(15),
                        Price = 4100,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 135,
                        Class = "economy",
                        IsReturn = false
                    }
                };

                // Если есть обратная дата, добавляем обратные рейсы
                if (request.ReturnDate.HasValue)
                {
                    flights.AddRange(new List<Flight>
                    {
                        new Flight
                        {
                            Id = "6",
                            Airline = "Аэрофлот",
                            FlightNumber = "SU-101",
                            DepartureCity = request.ArrivalCity,
                            ArrivalCity = request.DepartureCity,
                            DepartureAirport = "LED",
                            ArrivalAirport = "SVO",
                            DepartureTime = request.ReturnDate.Value.AddHours(10),
                            ArrivalTime = request.ReturnDate.Value.AddHours(12),
                            Price = 4600,
                            Currency = "RUB",
                            Transfers = 0,
                            Duration = 120,
                            Class = "economy",
                            IsReturn = true
                        },
                        new Flight
                        {
                            Id = "7",
                            Airline = "S7 Airlines",
                            FlightNumber = "S7-201",
                            DepartureCity = request.ArrivalCity,
                            ArrivalCity = request.DepartureCity,
                            DepartureAirport = "LED",
                            ArrivalAirport = "DME",
                            DepartureTime = request.ReturnDate.Value.AddHours(16),
                            ArrivalTime = request.ReturnDate.Value.AddHours(18),
                            Price = 5300,
                            Currency = "RUB",
                            Transfers = 0,
                            Duration = 120,
                            Class = "economy",
                            IsReturn = true
                        }
                    });
                }

                _logger.LogInformation("Сгенерировано демо-рейсов: {Count}", flights.Count);
                return flights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в упрощенном сервисе поиска");
                return new List<Flight>();
            }
        }

        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            try
            {
                _logger.LogInformation("Поиск городов по запросу: {Query}", query);

                // Имитируем небольшую задержку
                await Task.Delay(200);

                var cities = new List<City>
                {
                    new City { Code = "MOW", Name = "Москва", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "LED", Name = "Санкт-Петербург", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "SVX", Name = "Екатеринбург", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "KZN", Name = "Казань", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "SVO", Name = "Шереметьево", Country = "Россия", CountryCode = "RU", Type = "airport", Airport = "Шереметьево" },
                    new City { Code = "DME", Name = "Домодедово", Country = "Россия", CountryCode = "RU", Type = "airport", Airport = "Домодедово" },
                    new City { Code = "VKO", Name = "Внуково", Country = "Россия", CountryCode = "RU", Type = "airport", Airport = "Внуково" },
                    new City { Code = "OVB", Name = "Новосибирск", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "ROV", Name = "Ростов-на-Дону", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "AER", Name = "Сочи", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "KRR", Name = "Краснодар", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "UFA", Name = "Уфа", Country = "Россия", CountryCode = "RU", Type = "city" },
                    new City { Code = "KGD", Name = "Калининград", Country = "Россия", CountryCode = "RU", Type = "city" }
                };

                var result = cities.Where(c =>
                    c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();

                _logger.LogInformation("Найдено городов: {Count}", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов");
                return new List<City>();
            }
        }

        // Заглушки для неиспользуемых методов интерфейса
        public Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request)
        {
            return Task.FromResult(new AviasalesSearchResponseV2());
        }

        public Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0)
        {
            return Task.FromResult(new AviasalesResultsResponse());
        }

        public Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId)
        {
            return Task.FromResult(new ClickResponseV2());
        }
    }
}