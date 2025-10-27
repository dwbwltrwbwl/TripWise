using System.ComponentModel.DataAnnotations;

namespace TripWise.Models
{
    public class role
    {
        [Key]
        public int idRole { get; set; }
        [Required]
        [StringLength(50)]
        public string name { get; set; } = string.Empty;
        public virtual ICollection<user> Users { get; set; } = new List<user>();
    }
}
