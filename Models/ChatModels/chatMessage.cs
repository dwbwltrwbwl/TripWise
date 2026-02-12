using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models;

[Table("ChatMessages")]
public partial class ChatMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("idMessage")]
    public int IdMessage { get; set; }

    [Column("idChat")]
    public int ChatId { get; set; }

    [Column("idUser")]
    public int SenderId { get; set; }

    [Required]
    [Column("message")]
    public string Message { get; set; } = null!;

    [Column("sentAt")]
    public DateTime SentAt { get; set; }

    [Column("editedAt")]
    public DateTime? EditedAt { get; set; }

    [Column("replyToId")]
    public int? ReplyToId { get; set; }

    [Column("attachmentType")]
    [StringLength(50)]
    public string? AttachmentType { get; set; }

    [Column("attachmentUrl")]
    [StringLength(500)]
    public string? AttachmentUrl { get; set; }

    [Column("attachmentName")]
    [StringLength(255)]
    public string? AttachmentName { get; set; }

    [Column("attachmentSize")]
    public long? AttachmentSize { get; set; }

    // Старые поля для обратной совместимости
    [Column("idTrip")]
    public int? IdTrip { get; set; }

    [Column("idPoint")]
    public int? IdPoint { get; set; }

    // Навигационные свойства - УБИРАЕМ ДУБЛИРОВАНИЕ
    [ForeignKey("ChatId")]
    public virtual Chat? Chat { get; set; }

    // ОДИН навигационный property для пользователя
    [ForeignKey("SenderId")]
    public virtual User? Sender { get; set; }  // Только это, удалите IdUserNavigation!

    [ForeignKey("ReplyToId")]
    public virtual ChatMessage? ReplyTo { get; set; }

    public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();

    public virtual ICollection<ChatMessageRead> Reads { get; set; } = new List<ChatMessageRead>();

    // Старые навигационные свойства - переименовываем или удаляем
    [ForeignKey("IdTrip")]
    public virtual Trip? Trip { get; set; }  // Было IdTripNavigation

    [ForeignKey("IdPoint")]
    public virtual PointsOfInterest? Point { get; set; }  // Было IdPointNavigation
}