using Microsoft.EntityFrameworkCore;
using TripWise.Models;

namespace TripWise.Data
{
    public class TripWiseDbContext : DbContext
    {
        public TripWiseDbContext(DbContextOptions<TripWiseDbContext> options) : base(options) { }

        public DbSet<role> Roles { get; set; }
        public DbSet<user> Users { get; set; }
        public DbSet<trip> Trips { get; set; }
        public DbSet<tripParticipant> TripParticipants { get; set; }
        public DbSet<pointOfInterest> PointsOfInterest { get; set; }
        public DbSet<expense> Expenses { get; set; }
        public DbSet<expenseShare> ExpenseShares { get; set; }
        public DbSet<chatMessage> ChatMessages { get; set; }
        public DbSet<votingSystem> votingSystems { get; set; }
        public DbSet<voteOption> VoteOptions { get; set; }
        public DbSet<userVote> UserVotes { get; set; }
        public DbSet<document> Documents { get; set; }
    }
}
