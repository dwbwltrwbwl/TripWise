using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [ForeignKey("idExpense")]
        public virtual expense Expense { get; set; } = null!;
        [ForeignKey("idUser")]
        public virtual user User { get; set; } = null!;
    }
}
