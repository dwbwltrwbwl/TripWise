using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class votingSystem
    {
        [Key]
        public int IdVote { get; set; }
        [Required]
        [StringLength(200)]
        public string question { get; set; } = string.Empty;
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime? expiresAt { get; set; }
        public int idTrip { get; set; }
        public int createdById { get; set; }
        public int? idPoint { get; set; }
        [ForeignKey("idTrip")]
        public virtual trip Trip { get; set; } = null!;
        [ForeignKey("createdById")]
        public virtual user CreatedBy { get; set; } = null!;
        [ForeignKey("idPoint")]
        public virtual pointOfInterest? PointOfInterest { get; set; }
        public virtual ICollection<voteOption> VoteOptions { get; set; } = new List<voteOption>();
    }
}
