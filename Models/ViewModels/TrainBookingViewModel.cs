// Models/ViewModels/TrainBookingViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace TripWise.Models.ViewModels
{
    public class TrainBookingViewModel
    {
        [Required]
        public string TrainNumber { get; set; }

        public string? ReturnTrainNumber { get; set; }

        [Required]
        public string DepartureStationId { get; set; }

        [Required]
        public string DepartureStationName { get; set; }

        [Required]
        public string ArrivalStationId { get; set; }

        [Required]
        public string ArrivalStationName { get; set; }

        [Required]
        public DateTime DepartureDateTime { get; set; }

        public DateTime? ArrivalDateTime { get; set; }

        public DateTime? ReturnDepartureDateTime { get; set; }

        public DateTime? ReturnArrivalDateTime { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int Passengers { get; set; } = 1;

        [Required]
        public string CarType { get; set; }

        [Required]
        public string CarClass { get; set; }

        public int Duration { get; set; }

        public int? ReturnDuration { get; set; }

        public bool IsRoundTrip { get; set; }
    }

    public class PassengerInfoViewModel
    {
        [Required(ErrorMessage = "Укажите фамилию")]
        [Display(Name = "Фамилия")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Фамилия должна содержать от 2 до 50 символов")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Укажите имя")]
        [Display(Name = "Имя")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 50 символов")]
        public string FirstName { get; set; }

        [Display(Name = "Отчество")]
        [StringLength(50, ErrorMessage = "Отчество должно содержать до 50 символов")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Укажите дату рождения")]
        [Display(Name = "Дата рождения")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Укажите пол")]
        [Display(Name = "Пол")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Укажите тип документа")]
        [Display(Name = "Тип документа")]
        public string DocumentType { get; set; } = "passport";

        [Required(ErrorMessage = "Укажите номер документа")]
        [Display(Name = "Номер документа")]
        [StringLength(20, MinimumLength = 4, ErrorMessage = "Номер документа должен содержать от 4 до 20 символов")]
        public string DocumentNumber { get; set; }

        [Display(Name = "Гражданство")]
        public string Citizenship { get; set; } = "РФ";
    }

    public class ContactInfoViewModel
    {
        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        [Display(Name = "Телефон")]
        public string Phone { get; set; }

        [Display(Name = "Согласен с условиями")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Необходимо согласие с условиями")]
        public bool AgreeToTerms { get; set; }
    }

    public class CompleteBookingViewModel
    {
        public TrainBookingViewModel TrainInfo { get; set; }
        public PassengerInfoViewModel Passenger { get; set; }
        public ContactInfoViewModel Contact { get; set; }

        public decimal TotalPrice => TrainInfo.Price * TrainInfo.Passengers;
    }
}