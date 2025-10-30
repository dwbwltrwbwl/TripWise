using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class ChatMessage
{
    public int IdMessage { get; set; }

    public string Message { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public int IdTrip { get; set; }

    public int IdUser { get; set; }

    public int? IdPoint { get; set; }

    public virtual PointsOfInterest? IdPointNavigation { get; set; }

    public virtual Trip IdTripNavigation { get; set; } = null!;

    public virtual User IdUserNavigation { get; set; } = null!;
}
