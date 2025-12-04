using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                _logger.LogInformation("=== НАЧАЛО ПОИСКА РЕЙСОВ ===");
                _logger.LogInformation("Запрос получен:");
                _logger.LogInformation("- DepartureCity: {DepartureCity}", request.DepartureCity);
                _logger.LogInformation("- ArrivalCity: {ArrivalCity}", request.ArrivalCity);
                _logger.LogInformation("- DepartureDate: {DepartureDate}", request.DepartureDate);
                _logger.LogInformation("- ReturnDate: {ReturnDate}", request.ReturnDate);
                _logger.LogInformation("- ReturnDate.HasValue: {HasValue}", request.ReturnDate.HasValue);
                _logger.LogInformation("- ReturnDate.Value: {Value}", request.ReturnDate.HasValue ? request.ReturnDate.Value.ToString() : "null");
                _logger.LogInformation("- Passengers: {Passengers}", request.Passengers);
                _logger.LogInformation("- Class: {Class}", request.Class);
                _logger.LogInformation("- TripType: {TripType}", request.TripType);

                // Имитируем небольшую задержку как у реального API
                await Task.Delay(500);

                var allFlights = new List<Flight>();

                // 1. Генерируем рейсы ТУДА
                _logger.LogInformation("Генерация рейсов ТУДА...");
                var oneWayFlights = GenerateOneWayFlights(request);
                _logger.LogInformation("Сгенерировано рейсов ТУДА: {Count}", oneWayFlights.Count);
                allFlights.AddRange(oneWayFlights);

                // 2. Генерируем рейсы ОБРАТНО (только если указана обратная дата)
                if (request.ReturnDate.HasValue && request.ReturnDate.Value > DateTime.MinValue)
                {
                    _logger.LogInformation("Генерация рейсов ОБРАТНО для даты {ReturnDate}...", request.ReturnDate.Value);
                    var returnFlights = GenerateReturnFlights(request);
                    _logger.LogInformation("Сгенерировано рейсов ОБРАТНО: {Count}", returnFlights.Count);
                    allFlights.AddRange(returnFlights);
                }
                else
                {
                    _logger.LogInformation("Обратная дата не указана или равна MinValue - генерируем только рейсы туда");
                }

                // Логируем детали по каждому рейсу
                _logger.LogInformation("=== ДЕТАЛИ РЕЙСОВ ===");
                foreach (var flight in allFlights)
                {
                    _logger.LogInformation(
                        "Рейс {Id}: {Airline} {FlightNumber}, " +
                        "Направление: {FromCity} ({FromAirport}) -> {ToCity} ({ToAirport}), " +
                        "Вылет: {DepartureTime}, Прилет: {ArrivalTime}, " +
                        "Цена: {Price} {Currency}, Тип: {Type}, IsReturn: {IsReturn}",
                        flight.Id,
                        flight.Airline,
                        flight.FlightNumber,
                        flight.DepartureCity,
                        flight.DepartureAirport,
                        flight.ArrivalCity,
                        flight.ArrivalAirport,
                        flight.DepartureTime.ToString("yyyy-MM-dd HH:mm"),
                        flight.ArrivalTime.ToString("yyyy-MM-dd HH:mm"),
                        flight.Price,
                        flight.Currency,
                        flight.IsReturn ? "Обратно" : "Туда",
                        flight.IsReturn
                    );
                }

                _logger.LogInformation("=== ВСЕГО РЕЙСОВ: {TotalCount} ===", allFlights.Count);
                _logger.LogInformation("Рейсы туда: {OneWayCount}", oneWayFlights.Count);
                _logger.LogInformation("Рейсы обратно: {ReturnCount}", allFlights.Count - oneWayFlights.Count);

                return allFlights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в упрощенном сервисе поиска");
                // В случае ошибки возвращаем пустой список
                return new List<Flight>();
            }
        }

        private List<Flight> GenerateOneWayFlights(FlightSearchRequest request)
        {
            var flights = new List<Flight>
            {
                new Flight
                {
                    Id = "T1",
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
                    Id = "T2",
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
                    Id = "T3",
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
                    Id = "T4",
                    Airline = "Ural Airlines",
                    FlightNumber = "U6-400",
                    DepartureCity = request.DepartureCity,
                    ArrivalCity = request.ArrivalCity,
                    DepartureAirport = "SVO",
                    ArrivalAirport = "LED",
                    DepartureTime = request.DepartureDate.AddHours(18),
                    ArrivalTime = request.DepartureDate.AddHours(20),
                    Price = 3800,
                    Currency = "RUB",
                    Transfers = 1,
                    Duration = 150,
                    Class = "economy",
                    IsReturn = false
                }
            };

            return flights;
        }

        private List<Flight> GenerateReturnFlights(FlightSearchRequest request)
        {
            if (!request.ReturnDate.HasValue || request.ReturnDate.Value <= DateTime.MinValue)
            {
                _logger.LogWarning("ReturnDate не имеет значения или равно MinValue");
                return new List<Flight>();
            }

            var flights = new List<Flight>
            {
                new Flight
                {
                    Id = "R1",
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
                    Id = "R2",
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
                },
                new Flight
                {
                    Id = "R3",
                    Airline = "Победа",
                    FlightNumber = "DP-301",
                    DepartureCity = request.ArrivalCity,
                    ArrivalCity = request.DepartureCity,
                    DepartureAirport = "LED",
                    ArrivalAirport = "VKO",
                    DepartureTime = request.ReturnDate.Value.AddHours(8),
                    ArrivalTime = request.ReturnDate.Value.AddHours(10),
                    Price = 3300,
                    Currency = "RUB",
                    Transfers = 0,
                    Duration = 120,
                    Class = "economy",
                    IsReturn = true
                },
                new Flight
                {
                    Id = "R4",
                    Airline = "Ural Airlines",
                    FlightNumber = "U6-401",
                    DepartureCity = request.ArrivalCity,
                    ArrivalCity = request.DepartureCity,
                    DepartureAirport = "LED",
                    ArrivalAirport = "SVO",
                    DepartureTime = request.ReturnDate.Value.AddHours(14),
                    ArrivalTime = request.ReturnDate.Value.AddHours(16),
                    Price = 4200,
                    Currency = "RUB",
                    Transfers = 1,
                    Duration = 150,
                    Class = "economy",
                    IsReturn = true
                }
            };

            return flights;
        }

        // Остальной код остается без изменений
        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            try
            {
                _logger.LogInformation("Поиск городов по запросу: {Query}", query);

                // Имитируем небольшую задержку
                await Task.Delay(200);

                var cities = GetCitiesData();

                var result = cities.Where(c =>
                    c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (c.Airport != null && c.Airport.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    .Take(15)
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

        private List<City> GetCitiesData()
        {
            return new List<City>
            {
                new City { Code = "MOW", Name = "Москва", Country = "Россия", CountryCode = "RU", Type = "city" },
                new City { Code = "LED", Name = "Санкт-Петербург", Country = "Россия", CountryCode = "RU", Type = "city" },
                new City { Code = "SVX", Name = "Екатеринбург", Country = "Россия", CountryCode = "RU", Type = "city" },
                new City { Code = "KZN", Name = "Казань", Country = "Россия", CountryCode = "RU", Type = "city" },
                new City { Code = "SVO", Name = "Шереметьево", Country = "Россия", CountryCode = "RU", Type = "airport", Airport = "Шереметьево" },
                new City { Code = "DME", Name = "Домодедово", Country = "Россия", CountryCode = "RU", Type = "airport", Airport = "Домодедово" },
                new City { Code = "VKO", Name = "Внуково", Country = "Россия", CountryCode = "RU", Type = "airport", Airport = "Внуково" },
                new City { Code = "AER", Name = "Сочи", Country = "Россия", CountryCode = "RU", Type = "city" },
                new City { Code = "KRR", Name = "Краснодар", Country = "Россия", CountryCode = "RU", Type = "city" },
                new City { Code = "OVB", Name = "Новосибирск", Country = "Россия", CountryCode = "RU", Type = "city" }
            };
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