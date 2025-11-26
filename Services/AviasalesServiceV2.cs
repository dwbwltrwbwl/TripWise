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
                _logger.LogInformation("Запрос: {@Request}", request);
                _logger.LogInformation("ReturnDate HasValue: {HasValue}, Value: {Value}",
                    request.ReturnDate.HasValue,
                    request.ReturnDate.HasValue ? request.ReturnDate.Value : DateTime.MinValue);

                // Имитируем небольшую задержку как у реального API
                await Task.Delay(500);

                var flights = new List<Flight>();

                // Рейсы ТУДА
                var oneWayFlights = GenerateOneWayFlights(request);
                flights.AddRange(oneWayFlights);
                _logger.LogInformation("Сгенерировано рейсов ТУДА: {Count}", oneWayFlights.Count);

                // Рейсы ОБРАТНО (только если есть обратная дата)
                if (request.ReturnDate.HasValue)
                {
                    _logger.LogInformation("Генерация рейсов ОБРАТНО...");
                    var returnFlights = GenerateReturnFlights(request);
                    _logger.LogInformation("Сгенерировано рейсов ОБРАТНО: {Count}", returnFlights.Count);
                    flights.AddRange(returnFlights);

                    _logger.LogInformation("Всего рейсов: {TotalCount} (туда: {OneWayCount}, обратно: {ReturnCount})",
                        flights.Count, oneWayFlights.Count, returnFlights.Count);
                }
                else
                {
                    _logger.LogInformation("Обратная дата не указана, рейсы только туда: {Count}", flights.Count);
                }

                // Логируем все рейсы для отладки
                foreach (var flight in flights)
                {
                    _logger.LogInformation("Рейс: {Id}, Airline: {Airline}, IsReturn: {IsReturn}, From: {From} -> {To}",
                        flight.Id, flight.Airline, flight.IsReturn, flight.DepartureCity, flight.ArrivalCity);
                }

                return flights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в упрощенном сервисе поиска");
                return new List<Flight>();
            }
        }

        private List<Flight> GenerateOneWayFlights(FlightSearchRequest request)
        {
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
                }
            };

            return flights;
        }

        private List<Flight> GenerateReturnFlights(FlightSearchRequest request)
        {
            if (!request.ReturnDate.HasValue)
                return new List<Flight>();

            var flights = new List<Flight>
            {
                new Flight
                {
                    Id = "10",
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
                    Id = "11",
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
                    Id = "12",
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
                }
            };

            return flights;
        }

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
                new City { Code = "TJM", Name = "Тюмень", Country = "Россия", CountryCode = "RU", Type = "city" }
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