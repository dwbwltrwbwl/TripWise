using System.ComponentModel.DataAnnotations;

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
    }
}
