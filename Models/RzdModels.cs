namespace TripWise.Models
{
    public class RzdApiRequest
    {
        public string Code0 { get; set; } // станция отправления
        public string Code1 { get; set; } // станция прибытия
        public string Dt0 { get; set; }   // дата отправления
        public int Dir { get; set; } = 0; // 0 - в одну сторону
        public int Tfl { get; set; } = 3; // 3 - поезда и электрички
        public int CheckSeats { get; set; } = 1; // 1 - только с билетами
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
        public string Date0 { get; set; } // дата отправления
        public string Date1 { get; set; } // дата прибытия
        public string Time0 { get; set; } // время отправления
        public string Time1 { get; set; } // время прибытия
        public string Route0 { get; set; } // станция отправления
        public string Route1 { get; set; } // станция прибытия
        public string Number { get; set; } // номер поезда
        public string TimeInWay { get; set; } // время в пути
        public string Brand { get; set; } // название поезда
        public string Carrier { get; set; } // перевозчик
        public List<RzdCar> Cars { get; set; } // вагоны
    }

    public class RzdCar
    {
        public string Type { get; set; } // тип вагона
        public string TypeLoc { get; set; } // полное название
        public string ServCls { get; set; } // класс обслуживания
        public int FreeSeats { get; set; } // свободные места
        public decimal Tariff { get; set; } // цена
    }

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
