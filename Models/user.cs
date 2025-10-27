using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace TripWise.Models
{
    public class user
    {
        [Key]
        public int idUser { get; set; }
        [Required]
        [StringLength(100)]
        public string name { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string email { get; set; } = string.Empty;
        [Required]
        public string passwordHash { get; set; } = string.Empty;
        [Range(0, 120)]
        public int? age { get; set; }
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public int idRole { get; set; }
        [ForeignKey("idRole")]
        public virtual role Role { get; set; } = null!;
        public virtual ICollection<tripParticipant> TripParticipants { get; set; } = new List<tripParticipant>();
        public virtual ICollection<expense> Expenses { get; set; } = new List<expense>();
        public virtual ICollection<chatMessage> ChatMessages { get; set; } = new List<chatMessage>();
        public virtual ICollection<votingSystem> VotingSystems { get; set; } = new List<votingSystem>();
    }
}
