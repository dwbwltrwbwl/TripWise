using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using TripWise.Models;
using System.Text.Json;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly IAviasalesRealService _aviasalesService;
        private readonly ILogger<FlightsController> _logger;

        public FlightsController(IAviasalesRealService aviasalesService, ILogger<FlightsController> logger)
        {
            _aviasalesService = aviasalesService;
            _logger = logger;
        }

        [HttpPost("search")]
        [HttpPost("search")]
        public async Task<ActionResult<FlightSearchResponse>> SearchFlights([FromBody] FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Получен запрос на поиск рейсов: {@Request}", request);

                // Валидация
                var validationError = ValidateFlightSearchRequest(request);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return BadRequest(new FlightSearchResponse
                    {
                        Success = false,
                        Error = validationError
                    });
                }

                var flights = await _aviasalesService.SearchFlightsAsync(request);

                _logger.LogInformation("Найдено рейсов: {Count}", flights.Count);

                return Ok(new FlightSearchResponse
                {
                    Success = true,
                    Flights = flights,
                    Message = flights.Count > 0 ? $"Найдено {flights.Count} рейсов" : "Рейсы не найдены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске авиабилетов");
                return Ok(new FlightSearchResponse
                {
                    Success = false,
                    Error = "Временная ошибка поиска",
                    Message = "Используются демо-данные для тестирования"
                });
            }
        }

        [HttpGet("cities")]
        public async Task<ActionResult<CitySearchResponse>> SearchCities([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new CitySearchResponse
                    {
                        Success = true,
                        Cities = new List<City>(),
                        Message = "Введите минимум 2 символа для поиска"
                    });
                }

                _logger.LogInformation("Поиск городов по запросу: {Query}", query);

                var cities = await _aviasalesService.SearchCitiesAsync(query);
                var result = cities.Take(15).ToList(); // Ограничиваем количество результатов

                _logger.LogInformation("Найдено городов: {Count}", result.Count);

                return Ok(new CitySearchResponse
                {
                    Success = true,
                    Cities = result,
                    Message = result.Count > 0 ? $"Найдено {result.Count} городов" : "Городы не найдены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов для запроса: {Query}", query);
                return Ok(new CitySearchResponse
                {
                    Success = false,
                    Cities = new List<City>(),
                    Error = "Ошибка при поиске городов"
                });
            }
        }

        //[HttpPost("start-search")]
        //public async Task<ActionResult> StartSearch([FromBody] FlightSearchRequest request)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Запуск асинхронного поиска: {@Request}", request);

        //        // Валидация
        //        var validationError = ValidateFlightSearchRequest(request);
        //        if (!string.IsNullOrEmpty(validationError))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                error = validationError
        //            });
        //        }

        //        var searchResponse = await _aviasalesService.StartSearchAsync(request);

        //        _logger.LogInformation("Асинхронный поиск запущен, SearchId: {SearchId}", searchResponse.SearchId);

        //        return Ok(new
        //        {
        //            success = true,
        //            searchId = searchResponse.SearchId,
        //            resultsUrl = searchResponse.ResultsUrl,
        //            message = "Поиск запущен успешно"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при старте асинхронного поиска");
        //        return StatusCode(500, new
        //        {
        //            success = false,
        //            error = "Не удалось запустить поиск",
        //            details = ex.Message
        //        });
        //    }
        //}

        //[HttpPost("get-results")]
        //public async Task<ActionResult> GetResults([FromBody] ResultsRequest request)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(request.SearchId))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                error = "SearchId обязателен"
        //            });
        //        }

        //        if (string.IsNullOrEmpty(request.ResultsUrl))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                error = "ResultsUrl обязателен"
        //            });
        //        }

        //        _logger.LogInformation("Получение результатов поиска: {SearchId}, LastUpdate: {LastUpdate}",
        //            request.SearchId, request.LastUpdateTimestamp);

        //        var results = await _aviasalesService.GetSearchResultsAsync(
        //            request.SearchId,
        //            request.ResultsUrl,
        //            request.LastUpdateTimestamp);

        //        if (results == null)
        //        {
        //            return Ok(new
        //            {
        //                success = false,
        //                error = "Не удалось получить результаты",
        //                isOver = true
        //            });
        //        }

        //        // Логируем детали результатов для диагностики
        //        _logger.LogInformation("Результаты поиска: Tickets={TicketsCount}, Airlines={AirlinesCount}, Airports={AirportsCount}, IsOver={IsOver}",
        //            results.Tickets?.Count ?? 0,
        //            results.Airlines?.Count ?? 0,
        //            results.Airports?.Count ?? 0,
        //            results.IsOver);

        //        var simplifiedResults = ConvertToSimplifiedResults(results);

        //        return Ok(new
        //        {
        //            success = true,
        //            results = simplifiedResults,
        //            isOver = results.IsOver,
        //            lastUpdateTimestamp = results.LastUpdateTimestamp,
        //            statistics = new
        //            {
        //                ticketsCount = results.Tickets?.Count ?? 0,
        //                airlinesCount = results.Airlines?.Count ?? 0,
        //                airportsCount = results.Airports?.Count ?? 0
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при получении результатов для SearchId: {SearchId}", request.SearchId);
        //        return StatusCode(500, new
        //        {
        //            success = false,
        //            error = "Ошибка при получении результатов",
        //            details = ex.Message
        //        });
        //    }
        //}

        //[HttpGet("booking-link")]
        //public async Task<ActionResult> GetBookingLink(
        //    [FromQuery] string searchId,
        //    [FromQuery] string resultsUrl,
        //    [FromQuery] string proposalId)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(searchId))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                error = "SearchId обязателен"
        //            });
        //        }

        //        if (string.IsNullOrEmpty(resultsUrl))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                error = "ResultsUrl обязателен"
        //            });
        //        }

        //        if (string.IsNullOrEmpty(proposalId))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                error = "ProposalId обязателен"
        //            });
        //        }

        //        _logger.LogInformation("Получение ссылки на бронирование: SearchId={SearchId}, ProposalId={ProposalId}",
        //            searchId, proposalId);

        //        var bookingLink = await _aviasalesService.GetBookingLinkAsync(resultsUrl, searchId, proposalId);

        //        if (bookingLink == null || string.IsNullOrEmpty(bookingLink.Url))
        //        {
        //            return Ok(new
        //            {
        //                success = false,
        //                error = "Не удалось получить ссылку на бронирование"
        //            });
        //        }

        //        _logger.LogInformation("Ссылка на бронирование получена успешно");

        //        return Ok(new
        //        {
        //            success = true,
        //            url = bookingLink.Url,
        //            method = bookingLink.Method,
        //            expireAt = DateTimeOffset.FromUnixTimeSeconds(bookingLink.ExpireAtUnixSec).DateTime
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при получении ссылки на бронирование: SearchId={SearchId}, ProposalId={ProposalId}",
        //            searchId, proposalId);
        //        return StatusCode(500, new
        //        {
        //            success = false,
        //            error = "Ошибка при получении ссылки на бронирование",
        //            details = ex.Message
        //        });
        //    }
        //}

        [HttpGet("test-connection")]
        public async Task<ActionResult> TestConnection()
        {
            try
            {
                _logger.LogInformation("Тестирование подключения к API");

                // Простой тест - поиск популярных городов
                var cities = await _aviasalesService.SearchCitiesAsync("Москва");

                return Ok(new
                {
                    success = true,
                    message = "Подключение к API работает нормально",
                    citiesCount = cities.Count,
                    sampleCities = cities.Take(3).Select(c => new { c.Name, c.Code, c.Type })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестировании подключения к API");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Ошибка подключения к API",
                    details = ex.Message
                });
            }
        }

        // Вспомогательные методы

        private string ValidateFlightSearchRequest(FlightSearchRequest request)
        {
            if (request == null)
                return "Запрос не может быть пустым";

            if (string.IsNullOrEmpty(request.DepartureCity))
                return "Город вылета обязателен";

            if (string.IsNullOrEmpty(request.ArrivalCity))
                return "Город прилета обязателен";

            if (request.DepartureDate < DateTime.Today)
                return "Дата вылета не может быть в прошлом";

            if (request.ReturnDate.HasValue && request.ReturnDate < request.DepartureDate)
                return "Дата возвращения не может быть раньше даты вылета";

            if (request.Passengers < 1 || request.Passengers > 9)
                return "Количество пассажиров должно быть от 1 до 9";

            if (request.DepartureDate > DateTime.Today.AddYears(1))
                return "Дата вылета не может быть более чем на год вперед";

            return null;
        }

        private object ConvertToSimplifiedResults(AviasalesResultsResponse results)
        {
            if (results == null) return new { tickets = new List<object>() };

            var simplifiedTickets = new List<object>();

            if (results.Tickets != null)
            {
                foreach (var ticket in results.Tickets.Take(50)) // Ограничиваем для производительности
                {
                    var cheapestProposal = ticket.Proposals?.OrderBy(p => p.Price?.Amount ?? decimal.MaxValue).FirstOrDefault();

                    simplifiedTickets.Add(new
                    {
                        id = ticket.Id,
                        signature = ticket.Signature,
                        minPrice = cheapestProposal?.Price?.Amount ?? 0,
                        currency = cheapestProposal?.Price?.Currency ?? "RUB",
                        segmentsCount = ticket.Segments?.Count ?? 0,
                        transfersCount = ticket.Segments?.Sum(s => s.Transfers?.Count ?? 0) ?? 0,
                        proposalsCount = ticket.Proposals?.Count ?? 0,
                        firstProposal = cheapestProposal != null ? new
                        {
                            id = cheapestProposal.Id,
                            price = cheapestProposal.Price,
                            agentId = cheapestProposal.AgentId
                        } : null
                    });
                }
            }

            return new
            {
                tickets = simplifiedTickets,
                airlines = results.Airlines?.Count ?? 0,
                airports = results.Airports?.Count ?? 0,
                agents = results.Agents?.Count ?? 0
            };
        }
    }

    public class ResultsRequest
    {
        public string SearchId { get; set; }
        public string ResultsUrl { get; set; }
        public long LastUpdateTimestamp { get; set; }
    }

    public class CitySearchResponse
    {
        public bool Success { get; set; }
        public List<City> Cities { get; set; } = new List<City>();
        public string Error { get; set; }
        public string Message { get; set; }
    }
}