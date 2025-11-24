using System.Text.Json.Serialization;

namespace TripWise.Models
{
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

    // Модели для HotelLook API
    public class HotelLookSearchResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("results")]
        public HotelLookResults Results { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }

    public class HotelLookResults
    {
        [JsonPropertyName("hotels")]
        public List<HotelLookHotel> Hotels { get; set; }
    }

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

        [JsonPropertyName("locationId")]
        public int LocationId { get; set; }

        [JsonPropertyName("hotelName")]
        public string HotelName { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("photosCount")]
        public int PhotosCount { get; set; }

        [JsonPropertyName("rating")]
        public decimal Rating { get; set; }

        [JsonPropertyName("location")]
        public HotelLookLocation Location { get; set; }
    }

    public class HotelLookLocation
    {
        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("hotelsCount")]
        public int HotelsCount { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("geo")]
        public HotelLookGeo Geo { get; set; }
    }

    public class HotelLookGeo
    {
        [JsonPropertyName("lat")]
        public decimal Lat { get; set; }

        [JsonPropertyName("lon")]
        public decimal Lon { get; set; }
    }

    // Модели для поиска городов
    public class HotelCityLookupResponse
    {
        [JsonPropertyName("results")]
        public HotelLookupResults Results { get; set; }
    }

    public class HotelLookupResults
    {
        [JsonPropertyName("hotels")]
        public List<object> Hotels { get; set; }

        [JsonPropertyName("locations")]
        public List<HotelLookupLocation> Locations { get; set; }
    }

    public class HotelLookupLocation
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        [JsonPropertyName("location")]
        public string LocationName { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("hotelsCount")]
        public int HotelsCount { get; set; }

        [JsonPropertyName("iata")]
        public List<string> Iata { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}