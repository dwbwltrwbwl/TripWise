namespace TripWise.Models.DTOs
{
    public class PinMessageRequest
    {
        public int MessageId { get; set; }
        public bool PinForAll { get; set; } // true - для всех, false - только для себя
    }
}