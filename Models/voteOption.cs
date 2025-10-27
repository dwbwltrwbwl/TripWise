using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [ForeignKey("idVote")]
        public virtual votingSystem votingSystem { get; set; } = null!;
        public virtual ICollection<userVote> UserVotes { get; set; } = new List<userVote>();
    }
}
