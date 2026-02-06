namespace TripWise.Models
{
    public class NewsletterSubscription
    {
        public int Id { get; set; }
        public string Email { get; set; } = null!;
        public DateTime SubscribedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? UnsubscribedAt { get; set; }
        public string? Source { get; set; } // "footer", "registration", etc.
    }
}