using System.ComponentModel.DataAnnotations;

namespace TripWise.Models
{
    public class expenseShare
    {
        [Key]
        public int idExpenseShare { get; set; }
        public int idExpense { get; set; }
        public int idUser { get; set; }
        public decimal shareAmount { get; set; }
        public bool isPaid { get; set; } = false;
    }
}
