using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models;

[Table("Chats")]
public class Chat
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("idChat")]
    public int IdChat { get; set; }

    [Required]
    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Column("description")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [Column("type")]
    [StringLength(20)]
    public string Type { get; set; } = "group";

    [Column("idTrip")]
    public int? IdTrip { get; set; }

    [Column("createdById")]
    public int CreatedById { get; set; }

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; }

    [Column("lastMessageAt")]
    public DateTime? LastMessageAt { get; set; }

    // ======================
    // Навигационные свойства
    // ======================

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();

    [ForeignKey(nameof(CreatedById))]
    public virtual User? Creator { get; set; }

    [ForeignKey(nameof(IdTrip))]
    public virtual Trip? Trip { get; set; }
}