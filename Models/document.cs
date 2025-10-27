using System.ComponentModel.DataAnnotations;

namespace TripWise.Models
{
    public class document
    {
        [Key]
        public int idDocument { get; set; }
        [Required]
        [StringLength(255)]
        public string fileName { get; set; } = string.Empty;
        [Required]
        [StringLength(50)]
        public string fileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        [StringLength(500)]
        public string? filePath { get; set; }
        [StringLength(500)]
        public string? description { get; set; }
        public DateTime uploadedAt { get; set; } = DateTime.UtcNow;
        public int idTrip { get; set; }
        public int uploadedById { get; set; }
    }
}
