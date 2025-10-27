using System.ComponentModel.DataAnnotations;

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
    }
}
