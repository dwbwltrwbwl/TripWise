using System;
using System.ComponentModel.DataAnnotations;

namespace TripWise.Models.DTOs
{
    public class PlannedActivityDto
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ActivityId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TimeSpan Time { get; set; }

        public string Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string Category { get; set; }

        public string Tags { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string Address { get; set; }
    }
}