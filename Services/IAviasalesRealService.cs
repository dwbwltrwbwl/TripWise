using TripWise.Models;

namespace TripWise.Services
{
    public interface IAviasalesRealService
    {
        Task<List<Flight>> SearchFlightsAsync(FlightSearchRequest request);
        Task<List<City>> SearchCitiesAsync(string query);
        Task<AviasalesSearchResponseV2> StartSearchAsync(FlightSearchRequest request);
        Task<AviasalesResultsResponse> GetSearchResultsAsync(string searchId, string resultsUrl, long lastUpdateTimestamp = 0);
        Task<ClickResponseV2> GetBookingLinkAsync(string resultsUrl, string searchId, string proposalId);
    }
}