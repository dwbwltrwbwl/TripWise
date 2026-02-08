using Microsoft.EntityFrameworkCore;
using TripWise.Models;

namespace TripWise.Services
{
    public class FlightOrderService : IFlightOrderService
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<FlightOrderService> _logger;

        public FlightOrderService(TripWiseContext context, ILogger<FlightOrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FlightOrderResponse> CreateOrderAsync(FlightOrderRequest request, int userId)
        {
            try
            {
                // Генерируем номера
                var orderNumber = await GenerateOrderNumber();
                var ticketNumber = await GenerateTicketNumber();
                var bookingReference = await GenerateBookingReference();

                // Создаем заказ
                var order = new FlightOrder
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    FlightId = request.FlightId,
                    SearchId = request.SearchId,
                    OrderNumber = orderNumber,

                    // Данные рейса
                    Airline = request.SelectedFlight?.Airline ?? "Демо Авиакомпания",
                    FlightNumber = request.SelectedFlight?.FlightNumber ?? "DEMO-123",
                    DepartureCity = request.SelectedFlight?.DepartureCity ?? "Москва",
                    ArrivalCity = request.SelectedFlight?.ArrivalCity ?? "Санкт-Петербург",
                    DepartureAirport = request.SelectedFlight?.DepartureAirport ?? "SVO",
                    ArrivalAirport = request.SelectedFlight?.ArrivalAirport ?? "LED",
                    DepartureTime = request.SelectedFlight?.DepartureTime ?? DateTime.Now.AddDays(1),
                    ArrivalTime = request.SelectedFlight?.ArrivalTime ?? DateTime.Now.AddDays(1).AddHours(2),
                    Price = request.SelectedFlight?.Price ?? 5000,
                    Transfers = request.SelectedFlight?.Transfers ?? 0,
                    IsReturn = request.SelectedFlight?.IsReturn ?? false,

                    // Контактные данные
                    ContactEmail = request.Contact.Email,
                    ContactPhone = request.Contact.Phone,

                    // Генерируемые данные
                    TicketNumber = ticketNumber,
                    BookingReference = bookingReference,

                    // Статусы
                    Status = "pending",
                    PaymentStatus = "pending",
                    PaymentMethod = request.Payment.Method,
                    CreatedAt = DateTime.UtcNow
                };

                // Добавляем пассажиров
                foreach (var passenger in request.Passengers)
                {
                    order.Passengers.Add(new FlightPassenger
                    {
                        FirstName = passenger.FirstName,
                        LastName = passenger.LastName,
                        MiddleName = passenger.MiddleName,
                        DateOfBirth = passenger.DateOfBirth,
                        Gender = passenger.Gender,
                        DocumentType = passenger.DocumentType,
                        DocumentNumber = passenger.DocumentNumber,
                        Nationality = passenger.Nationality,
                        Baggage = "1x23кг",
                        SeatNumber = GenerateSeatNumber()
                    });
                }

                // Сохраняем в БД
                _context.FlightOrders.Add(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Создан заказ #{orderNumber} для пользователя {userId}");

                return new FlightOrderResponse
                {
                    Success = true,
                    OrderId = order.Id,
                    OrderNumber = orderNumber,
                    Message = "Заказ успешно создан",
                    Order = order,
                    TotalPrice = order.Price * order.Passengers.Count,
                    TicketNumber = ticketNumber,
                    BookingReference = bookingReference,
                    Status = "pending",
                    CreatedAt = order.CreatedAt,
                    ConfirmationUrl = $"/api/flights/confirm/{order.Id}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании заказа");
                throw;
            }
        }

        public async Task<FlightOrderResponse> ProcessDemoPaymentAsync(FlightOrderRequest request, int userId)
        {
            // Демо-обработка платежа
            var orderResponse = await CreateOrderAsync(request, userId);

            if (orderResponse.Success)
            {
                // Имитация успешной оплаты
                var order = await GetOrderByIdAsync(orderResponse.OrderId);
                if (order != null)
                {
                    order.PaymentStatus = "paid";
                    order.Status = "confirmed";
                    order.ConfirmedAt = DateTime.UtcNow;
                    order.TransactionId = $"DEMO_{DateTime.Now:yyyyMMddHHmmss}";

                    // Генерируем демо-билет
                    order.TicketNumber = $"TKT{DateTime.Now:yyyyMMddHHmmss}";

                    await _context.SaveChangesAsync();

                    orderResponse.Status = "confirmed";
                    orderResponse.Message = "Оплата прошла успешно. Билет выписан!";
                }
            }

            return orderResponse;
        }

        public async Task<FlightOrder> GetOrderByIdAsync(string orderId)
        {
            return await _context.FlightOrders
                .Include(o => o.Passengers)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<List<FlightOrder>> GetUserOrdersAsync(int userId)
        {
            // Если userId = -1 (неавторизованный пользователь), возвращаем пустой список
            if (userId == -1)
            {
                return new List<FlightOrder>();
            }

            return await _context.FlightOrders
                .Include(o => o.Passengers)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> CancelOrderAsync(string orderId, int userId)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null || order.UserId != userId)
                return false;

            order.Status = "cancelled";
            order.PaymentStatus = "refunded";
            order.Notes = $"Отменен пользователем {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ConfirmPaymentAsync(string orderId, string transactionId)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null)
                return false;

            order.PaymentStatus = "paid";
            order.Status = "confirmed";
            order.ConfirmedAt = DateTime.UtcNow;
            order.TransactionId = transactionId;

            await _context.SaveChangesAsync();
            return true;
        }

        private string GenerateSeatNumber()
        {
            var rows = new[] { "A", "B", "C", "D", "E", "F" };
            var random = new Random();
            return $"{random.Next(1, 35)}{rows[random.Next(0, rows.Length)]}";
        }

        public async Task<string> GenerateTicketNumber()
        {
            var datePart = DateTime.Now.ToString("yyMMdd");
            var random = new Random();
            var randomPart = random.Next(100000, 999999);
            return $"TKT{datePart}{randomPart}";
        }

        public async Task<string> GenerateBookingReference()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new char[6];

            for (int i = 0; i < 6; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }

        public async Task<string> GenerateOrderNumber()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var count = await _context.FlightOrders
                .CountAsync(o => o.CreatedAt.Date == DateTime.UtcNow.Date);

            return $"FLT{datePart}{count + 1:0000}";
        }
    }
}