namespace TripWise.Models
{
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
}
