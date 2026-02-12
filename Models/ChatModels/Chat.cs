using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models;

[Table("Chats")]
public partial class Chat
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("idChat")]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [StringLength(20)]
    [Column("type")]
    public string Type { get; set; } = null!; // private, group, trip

    [Column("idTrip")]
    public int? TripId { get; set; }

    [Column("createdById")]
    public int CreatedById { get; set; }

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [Column("lastMessageAt")]
    public DateTime? LastMessageAt { get; set; }

    // Навигационные свойства
    [ForeignKey("TripId")]
    public virtual Trip? Trip { get; set; }

    [ForeignKey("CreatedById")]
    public virtual User Creator { get; set; } = null!;

    public virtual ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}