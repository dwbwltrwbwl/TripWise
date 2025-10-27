using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class expense
    {
        [Key]
        public int idExpense { get; set; }
        [Required]
        [StringLength(200)]
        public string title { get; set; } = string.Empty;
        [Required]
        public decimal amount { get; set; }
        [Required]
        [StringLength(50)]
        public string category { get; set; } = string.Empty;
        public DateTime expenseDate { get; set; }
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public int idTrip { get; set; }
        public int paidById { get; set; }
        public int? idPoint { get; set; }
        [ForeignKey("idTrip")]
        public virtual trip Trip { get; set; } = null!;
        [ForeignKey("paidById")]
        public virtual user PaidBy { get; set; } = null!;
        [ForeignKey("idPoint")]
        public virtual pointOfInterest? PointOfInterest { get; set; }
        public virtual ICollection<expenseShare> ExpenseShares { get; set; } = new List<expenseShare>();
    }
}
