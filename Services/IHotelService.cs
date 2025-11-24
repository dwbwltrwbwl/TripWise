using TripWise.Models;

namespace TripWise.Services
{
    public interface IHotelService
    {
        Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request);
        Task<List<City>> SearchHotelCitiesAsync(string query);
    }
}