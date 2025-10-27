using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class pointOfInterest
    {
        [Key]
        public int idPoint { get; set; }
        [Required]
        [StringLength(200)]
        public string name { get; set; } = string.Empty;
        public string? description { get; set; }
        public decimal latitude { get; set; }
        public decimal longitude { get; set; }
        [StringLength(255)]
        public string? address { get; set; }
        [Required]
        [StringLength(50)]
        public string category { get; set; } = string.Empty;
        public DateTime? plannedDate { get; set; }
        public TimeSpan? plannedTime { get; set; }
        public decimal? cost { get; set; }
        [StringLength(500)]
        public string? bookingLink { get; set; }
        public string? notes { get; set; }
        public int idTrip { get; set; }
        public int? addedById { get; set; }
        [ForeignKey("idTrip")]
        public virtual trip Trip { get; set; } = null!;
        [ForeignKey("addedById")]
        public virtual user? AddedBy { get; set; }
        public virtual ICollection<expense> Expenses { get; set; } = new List<expense>();
    }
}
