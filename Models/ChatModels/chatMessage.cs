using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    [Table("ChatMessages")]
    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("idMessage")]
        public int IdMessage { get; set; }

        [Required]
        [Column("message")]
        public string Message { get; set; } = null!;

        [Column("sentAt")]
        public DateTime SentAt { get; set; }

        [Column("idTrip")]  // Это просто поле, не навигационное свойство
        public int? IdTrip { get; set; }

        [Column("idUser")]
        public int SenderId { get; set; }

        [Column("idPoint")]  // Это просто поле, не навигационное свойство
        public int? IdPoint { get; set; }

        [Column("attachmentName")]
        [StringLength(255)]
        public string? AttachmentName { get; set; }

        [Column("attachmentSize")]
        public long? AttachmentSize { get; set; }

        [Column("attachmentType")]
        [StringLength(50)]
        public string? AttachmentType { get; set; }

        [Column("attachmentUrl")]
        [StringLength(500)]
        public string? AttachmentUrl { get; set; }

        [Column("editedAt")]
        public DateTime? EditedAt { get; set; }

        [Column("idChat")]
        public int ChatId { get; set; }

        [Column("replyToId")]
        public int? ReplyToId { get; set; }

        // ТОЛЬКО эти навигационные свойства - БЕЗ атрибутов ForeignKey
        public virtual Chat? Chat { get; set; }
        public virtual User? Sender { get; set; }
        public virtual ChatMessage? ReplyTo { get; set; }

        // ПОЛНОСТЬЮ УДАЛЯЕМ эти свойства
        // public virtual Trip? Trip { get; set; }
        // public virtual PointsOfInterest? Point { get; set; }

        public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
        public virtual ICollection<ChatMessageRead> Reads { get; set; } = new List<ChatMessageRead>();
    }
}