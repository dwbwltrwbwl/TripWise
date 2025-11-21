namespace TripWise.Models
{
    public class TrainSearchRequest
    {
        public string DepartureStationId { get; set; }
        public string ArrivalStationId { get; set; }
        public string DepartureDate { get; set; }
        public int Passengers { get; set; } = 1;
    }

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

    public class RzdApiRequest
    {
        public string Code0 { get; set; }
        public string Code1 { get; set; }
        public string Dt0 { get; set; }
        public int Dir { get; set; } = 0;
        public int Tfl { get; set; } = 3;
        public int CheckSeats { get; set; } = 1;
    }

    public class RzdApiResponse
    {
        public string Result { get; set; }
        public string Rid { get; set; }
        public string Timestamp { get; set; }
        public List<RzdRoute> Lst { get; set; }
    }

    public class RzdRoute
    {
        public string Date0 { get; set; }
        public string Date1 { get; set; }
        public string Time0 { get; set; }
        public string Time1 { get; set; }
        public string Route0 { get; set; }
        public string Route1 { get; set; }
        public string Number { get; set; }
        public string TimeInWay { get; set; }
        public string Brand { get; set; }
        public string Carrier { get; set; }
        public List<RzdCar> Cars { get; set; }
    }

    public class RzdCar
    {
        public string Type { get; set; }
        public string TypeLoc { get; set; }
        public string ServCls { get; set; }
        public int FreeSeats { get; set; }
        public decimal Tariff { get; set; }
    }

    public class Station
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Region { get; set; }
    }
}