using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using TripWise.Models;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly IAviasalesServiceV2 _aviasalesService;
        private readonly ILogger<FlightsController> _logger;

        public FlightsController(IAviasalesServiceV2 aviasalesService, ILogger<FlightsController> logger)
        {
            _aviasalesService = aviasalesService;
            _logger = logger;
        }

        [HttpPost("search")]
        public async Task<ActionResult> SearchFlights([FromBody] FlightSearchRequest request)
        {
            try
            {
                var flights = await _aviasalesService.SearchFlightsAsync(request);
                return Ok(new { success = true, flights });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске авиабилетов");
                return StatusCode(500, new { success = false, error = "Ошибка при поиске авиабилетов" });
            }
        }

        [HttpPost("start-search")]
        public async Task<ActionResult> StartSearch([FromBody] FlightSearchRequest request)
        {
            try
            {
                var searchResponse = await _aviasalesService.StartSearchAsync(request);
                return Ok(new { success = true, searchId = searchResponse.SearchId, resultsUrl = searchResponse.ResultsUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при старте поиска");
                return StatusCode(500, new { success = false, error = "Ошибка при старте поиска" });
            }
        }

        [HttpPost("get-results")]
        public async Task<ActionResult> GetResults([FromBody] ResultsRequest request)
        {
            try
            {
                var results = await _aviasalesService.GetSearchResultsAsync(request.SearchId, request.ResultsUrl, request.LastUpdateTimestamp);
                return Ok(new { success = true, results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении результатов");
                return StatusCode(500, new { success = false, error = "Ошибка при получении результатов" });
            }
        }

        [HttpGet("booking-link")]
        public async Task<ActionResult> GetBookingLink([FromQuery] string searchId, [FromQuery] string resultsUrl, [FromQuery] string proposalId)
        {
            try
            {
                var bookingLink = await _aviasalesService.GetBookingLinkAsync(resultsUrl, searchId, proposalId);
                return Ok(new { success = true, bookingLink });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении ссылки на бронирование");
                return StatusCode(500, new { success = false, error = "Ошибка при получении ссылки на бронирование" });
            }
        }
    }

    public class ResultsRequest
    {
        public string SearchId { get; set; }
        public string ResultsUrl { get; set; }
        public long LastUpdateTimestamp { get; set; }
    }
}
