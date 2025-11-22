namespace TripWise.Models
{
    public class FlightSearchRequest
    {
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public DateTime DepartureDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public int Passengers { get; set; } = 1;
        public string Class { get; set; } = "economy";
        public string TripType { get; set; } = "round";
    }

    public class FlightSearchResponse
    {
        public bool Success { get; set; }
        public List<Flight> Flights { get; set; } = new List<Flight>();
        public string Error { get; set; }
        public string SearchId { get; set; }
    }

    public class Flight
    {
        public string Id { get; set; }
        public string Airline { get; set; }
        public string FlightNumber { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "RUB";
        public int Transfers { get; set; }
        public int Duration { get; set; }
        public string Class { get; set; }
        public bool IsReturn { get; set; }
    }

    public class City
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Airport { get; set; }
        public string Type { get; set; }
    }
}