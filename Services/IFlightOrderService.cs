using TripWise.Models;

namespace TripWise.Services
{
    public interface IFlightOrderService
    {
        Task<FlightOrderResponse> CreateOrderAsync(FlightOrderRequest request, int userId);
        Task<FlightOrder> GetOrderByIdAsync(string orderId);
        Task<List<FlightOrder>> GetUserOrdersAsync(int userId);
        Task<bool> CancelOrderAsync(string orderId, int userId);
        Task<bool> ConfirmPaymentAsync(string orderId, string transactionId);
        Task<string> GenerateTicketNumber();
        Task<string> GenerateBookingReference();
        Task<string> GenerateOrderNumber();
        Task<FlightOrderResponse> ProcessDemoPaymentAsync(FlightOrderRequest request, int userId);
    }
}