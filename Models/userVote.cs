using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class userVote
    {
        [Key]
        public int idUserVote { get; set; }
        public int idVoteOption { get; set; }
        public int idUser { get; set; }
        public DateTime votedAt { get; set; } = DateTime.UtcNow;
        [ForeignKey("idVoteOption")]
        public virtual voteOption VoteOption { get; set; } = null!;
        [ForeignKey("idUser")]
        public virtual user User { get; set; } = null!;
    }
}
