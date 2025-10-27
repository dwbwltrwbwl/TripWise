using System.ComponentModel.DataAnnotations;

namespace TripWise.Models
{
    public class chatMessage
    {
        [Key]
        public int idMessage { get; set; }
        [Required]
        public string message { get; set; } = string.Empty;
        public DateTime sentAt { get; set; } = DateTime.UtcNow;
    }
}
