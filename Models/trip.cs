using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class trip
    {
        [Key]
        public int idTrip { get; set; }
        [Required]
        [StringLength(200)]
        public string title { get; set; } = string.Empty;
        public string? description { get; set; }
        [Required]
        public DateTime startDate { get; set; }
        [Required]
        public DateTime endDate { get; set; }
        public decimal totalBudget { get; set; }
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public int createdById { get; set; }
        [ForeignKey("createdById")]
        public virtual user createdBy { get; set; } = null!;
        public virtual ICollection<tripParticipant> TripParticipants { get; set; } = new List<tripParticipant>();
        public virtual ICollection<pointOfInterest> PointOfInterests { get; set; } = new List<pointOfInterest>();
        public virtual ICollection<expense> Expenses { get; set; } = new List<expense>();
        public virtual ICollection<chatMessage> ChatMessages { get; set; } = new List<chatMessage>();
        public virtual ICollection<votingSystem> VotingSystems { get; set; } = new List<votingSystem>();
        public virtual ICollection<document> Documents { get; set; } = new List<document>();
    }
}
