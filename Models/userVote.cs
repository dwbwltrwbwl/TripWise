using System.ComponentModel.DataAnnotations;

namespace TripWise.Models
{
    public class userVote
    {
        [Key]
        public int idUserVote { get; set; }
        public int idVoteOption { get; set; }
        public int idUser { get; set; }
        public DateTime votedAt { get; set; } = DateTime.UtcNow;
    }
}
