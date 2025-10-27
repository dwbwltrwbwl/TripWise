using System.ComponentModel.DataAnnotations;

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
    }
}
