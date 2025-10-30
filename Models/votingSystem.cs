using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class VotingSystem
{
    public int IdVote { get; set; }

    public string Question { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public int IdTrip { get; set; }

    public int CreatedById { get; set; }

    public int? IdPoint { get; set; }

    public virtual User CreatedBy { get; set; } = null!;

    public virtual PointsOfInterest? IdPointNavigation { get; set; }

    public virtual Trip IdTripNavigation { get; set; } = null!;

    public virtual ICollection<VoteOption> VoteOptions { get; set; } = new List<VoteOption>();
}
