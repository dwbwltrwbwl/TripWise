using System.ComponentModel.DataAnnotations;

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
    }
}
