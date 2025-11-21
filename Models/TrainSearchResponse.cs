namespace TripWise.Models
{
    public class TrainSearchResponse
    {
        public string Name { get; set; }
        public string DepartureStation { get; set; }
        public string ArrivalStation { get; set; }
        public string DepartureTime { get; set; }
        public string ArrivalTime { get; set; }
        public string TrainNumber { get; set; }
        public string TravelTime { get; set; }
        public List<TrainCategory> Categories { get; set; }
        public bool Firm { get; set; }
    }

    public class TrainCategory
    {
        public string Type { get; set; }
        public decimal Price { get; set; }
    }
}