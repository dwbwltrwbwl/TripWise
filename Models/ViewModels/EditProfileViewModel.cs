using System.ComponentModel.DataAnnotations;

namespace TripWise.Models.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Имя обязательно")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        public string Email { get; set; } = string.Empty;

        [Range(1, 120, ErrorMessage = "Возраст указан некорректно")]
        public int? Age { get; set; }
    }
}
