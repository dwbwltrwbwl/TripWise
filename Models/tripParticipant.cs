using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class tripParticipant
    {
        [Key]
        public int idTripParticipant { get; set; }
        public int idTrip { get; set; }
        public int idUser { get; set; }
        [Required]
        [StringLength(20)]
        public string participantRole { get; set; } = "Viewer";
        public DateTime joinedAt { get; set; } = DateTime.UtcNow;
        [ForeignKey("idTrip")]
        public virtual trip Trip { get; set; } = null!;
        [ForeignKey("idUser")]
        public virtual user User { get; set; } = null!;
    }
}
