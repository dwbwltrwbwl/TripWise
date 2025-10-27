using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class chatMessage
    {
        [Key]
        public int idMessage { get; set; }
        [Required]
        public string message { get; set; } = string.Empty;
        public DateTime sentAt { get; set; } = DateTime.UtcNow;
        public int idTrip { get; set; }
        public int idUser { get; set; }
        public int? idPoint { get; set; }
        [ForeignKey("idTrip")]
        public virtual trip Trip { get; set; } = null!;
        [ForeignKey("idUser")]
        public virtual user User { get; set; } = null!;
        [ForeignKey("idPoint")]
        public virtual pointOfInterest? PointOfInterest { get; set; }
    }
}
