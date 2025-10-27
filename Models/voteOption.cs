using System.ComponentModel.DataAnnotations;

namespace TripWise.Models
{
    public class voteOption
    {
        [Key]
        public int idVoteOption { get; set; }
        [Required]
        [StringLength(200)]
        public string optionText { get; set; } = string.Empty;
        public int idVote { get; set; }
    }
}
