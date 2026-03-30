// Models/Note.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class Note
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";

        [StringLength(5000)]
        public string Content { get; set; } = "";

        public string? Color { get; set; } // Цвет заметки (для визуального выделения)

        public bool IsPinned { get; set; } // Закрепленная заметка

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
        public virtual ICollection<ChecklistItem>? ChecklistItems { get; set; }
    }
}