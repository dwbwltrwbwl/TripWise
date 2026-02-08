using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class FlightOrder
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        public string FlightId { get; set; }

        public string SearchId { get; set; }

        [Required]
        public string OrderNumber { get; set; }

        // Данные рейса
        [Required]
        public string Airline { get; set; }

        [Required]
        public string FlightNumber { get; set; }

        [Required]
        public string DepartureCity { get; set; }

        [Required]
        public string ArrivalCity { get; set; }

        [Required]
        public string DepartureAirport { get; set; }

        [Required]
        public string ArrivalAirport { get; set; }

        [Required]
        public DateTime DepartureTime { get; set; }

        [Required]
        public DateTime ArrivalTime { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        public string Currency { get; set; } = "RUB";

        [Required]
        public int Transfers { get; set; }

        [Required]
        public bool IsReturn { get; set; }

        // Контактные данные
        [Required]
        [EmailAddress]
        public string ContactEmail { get; set; }

        [Required]
        public string ContactPhone { get; set; }

        // Статус
        [Required]
        public string Status { get; set; } = "pending"; // pending, confirmed, cancelled, completed

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedAt { get; set; }

        // Оплата
        [Required]
        public string PaymentStatus { get; set; } = "pending"; // pending, paid, refunded, failed

        public string PaymentMethod { get; set; }

        public string TransactionId { get; set; }

        // Дополнительно
        public string BookingReference { get; set; }

        public string TicketNumber { get; set; }

        public string Notes { get; set; }

        // Навигационное свойство
        public List<FlightPassenger> Passengers { get; set; } = new List<FlightPassenger>();
    }

    public class FlightPassenger
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OrderId { get; set; }

        [ForeignKey("OrderId")]
        public FlightOrder Order { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string MiddleName { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(1)]
        public string Gender { get; set; } // M, F

        [Required]
        public string DocumentType { get; set; } = "passport";

        [Required]
        public string DocumentNumber { get; set; }

        [Required]
        public string Nationality { get; set; }

        public string SeatNumber { get; set; }

        public string Baggage { get; set; }

        public string MealPreference { get; set; }
    }

    public class FlightOrderRequest
    {
        [Required]
        public string FlightId { get; set; }

        public string SearchId { get; set; }

        public Flight SelectedFlight { get; set; }

        [Required]
        [MinLength(1)]
        public List<Passenger> Passengers { get; set; } = new List<Passenger>();

        [Required]
        public ContactInfo Contact { get; set; }

        [Required]
        public PaymentInfo Payment { get; set; }
    }

    public class Passenger
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string MiddleName { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [RegularExpression("^[MF]$", ErrorMessage = "Gender must be 'M' or 'F'")]
        public string Gender { get; set; }

        [Required]
        public string DocumentType { get; set; } = "passport";

        [Required]
        public string DocumentNumber { get; set; }

        [Required]
        public string Nationality { get; set; }
    }

    public class ContactInfo
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Phone]
        public string Phone { get; set; }
    }

    public class PaymentInfo
    {
        [Required]
        public string Method { get; set; } // card, bank_transfer, etc.

        [Required]
        public string CardNumber { get; set; }

        [Required]
        public string CardHolder { get; set; }

        [Required]
        public string ExpiryMonth { get; set; }

        [Required]
        public string ExpiryYear { get; set; }

        [Required]
        public string CVV { get; set; }
    }

    public class FlightOrderResponse
    {
        public bool Success { get; set; }
        public string OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string Message { get; set; }
        public FlightOrder Order { get; set; }
        public decimal TotalPrice { get; set; }
        public string TicketNumber { get; set; }
        public string BookingReference { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ConfirmationUrl { get; set; }
    }
}