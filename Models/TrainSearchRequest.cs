namespace TripWise.Models
{
    public class TrainSearchRequest
    {
        public string DepartureStationId { get; set; }
        public string ArrivalStationId { get; set; }
        public string DepartureDate { get; set; }
        public int Passengers { get; set; } = 1;
    }
}