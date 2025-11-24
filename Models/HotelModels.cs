using System.Text.Json.Serialization;

namespace TripWise.Models
{
    // ОСНОВНЫЕ МОДЕЛИ ДЛЯ ФРОНТЕНДА
    public class HotelSearchRequest
    {
        public string City { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Adults { get; set; } = 2;
        public int Rooms { get; set; } = 1;
        public int Children { get; set; } = 0;
        public string ChildrenAges { get; set; } = "";
    }

    public class HotelSearchResponse
    {
        public bool Success { get; set; }
        public List<Hotel> Hotels { get; set; } = new List<Hotel>();
        public string Error { get; set; }
        public string Message { get; set; }
    }

    public class Hotel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "RUB";
        public decimal Rating { get; set; }
        public int Stars { get; set; }
        public string Description { get; set; }
        public List<string> Photos { get; set; } = new List<string>();
        public List<string> Amenities { get; set; } = new List<string>();
        public Location Location { get; set; }
    }

    public class Location
    {
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }

    // МОДЕЛИ ДЛЯ РОССИЙСКИХ API
    public class SletatResponse
    {
        [JsonPropertyName("hotels")]
        public List<SletatHotel> Hotels { get; set; }
    }

    public class SletatHotel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        [JsonPropertyName("rating")]
        public decimal Rating { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("photos")]
        public List<string> Photos { get; set; }

        [JsonPropertyName("amenities")]
        public List<string> Amenities { get; set; }
    }

    public class SletatCity
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }

    // Модели для Tvil API
    public class TvilResponse
    {
        [JsonPropertyName("results")]
        public List<TvilHotel> Results { get; set; }
    }

    public class TvilHotel
    {
        [JsonPropertyName("hotelId")]
        public int HotelId { get; set; }

        [JsonPropertyName("hotelName")]
        public string HotelName { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("minPrice")]
        public decimal MinPrice { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        [JsonPropertyName("rating")]
        public decimal Rating { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("images")]
        public List<string> Images { get; set; }

        [JsonPropertyName("facilities")]
        public List<string> Facilities { get; set; }
    }

    public class TvilCityResponse
    {
        [JsonPropertyName("cities")]
        public List<TvilCity> Cities { get; set; }
    }

    public class TvilCity
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }

    // Модели для HotelLook (российская версия)
    public class HotelLookHotel
    {
        [JsonPropertyName("hotelId")]
        public int HotelId { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("priceAvg")]
        public decimal PriceAvg { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        [JsonPropertyName("hotelName")]
        public string HotelName { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("photosCount")]
        public int PhotosCount { get; set; }

        [JsonPropertyName("rating")]
        public decimal Rating { get; set; }
    }
}