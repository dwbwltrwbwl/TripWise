using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models;

[Table("ChatMembers")]
public partial class ChatMember
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("idChatMember")]
    public int Id { get; set; }

    [Column("idChat")]
    public int ChatId { get; set; }

    [Column("idUser")]
    public int UserId { get; set; }

    [Column("joinedAt")]
    public DateTime JoinedAt { get; set; }

    [Column("lastReadAt")]
    public DateTime? LastReadAt { get; set; }

    [Required]
    [StringLength(20)]
    [Column("role")]
    public string Role { get; set; } = "member"; // admin, member

    // Навигационные свойства
    [ForeignKey("ChatId")]
    public virtual Chat Chat { get; set; } = null!;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}