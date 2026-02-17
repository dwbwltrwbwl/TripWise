// Controllers/FlightBookingController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.ViewModels;
using TripWise.Services;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TripWise.Controllers
{
    public class FlightBookingController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<FlightBookingController> _logger;
        private readonly IMemoryCache _cache;

        public FlightBookingController(
            TripWiseContext context,
            EmailService emailService,
            ILogger<FlightBookingController> logger,
            IMemoryCache memoryCache)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cache = memoryCache;
        }

        // GET: /FlightBooking/Book
        [HttpGet]
        public IActionResult Book([FromQuery] FlightBookingViewModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.FlightId))
            {
                return RedirectToAction("Index", "Flights");
            }

            // Создаем ViewModel для формы
            var viewModel = new CompleteFlightBookingViewModel
            {
                Flight = model,
                Passengers = new List<FlightPassengerViewModel>(),
                Contact = new FlightContactViewModel()
            };

            // Добавляем одного пассажира по умолчанию
            for (int i = 0; i < model.Passengers; i++)
            {
                viewModel.Passengers.Add(new FlightPassengerViewModel());
            }

            // Если пользователь авторизован, подставляем его данные
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var user = _context.Users.Find(userId.Value);
                if (user != null)
                {
                    viewModel.Contact.Email = user.Email;
                    viewModel.Contact.Name = $"{user.FirstName} {user.LastName}".Trim();

                    // Подставляем данные первого пассажира из профиля
                    if (viewModel.Passengers.Count > 0)
                    {
                        viewModel.Passengers[0].FirstName = user.FirstName ?? "";
                        viewModel.Passengers[0].LastName = user.LastName ?? "";
                        viewModel.Passengers[0].MiddleName = user.MiddleName;
                    }
                }
            }

            return View(viewModel);
        }

        // POST: /FlightBooking/ProcessBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBooking([FromBody] CompleteFlightBookingViewModel model)
        {
            try
            {
                _logger.LogInformation("=== НАЧАЛО БРОНИРОВАНИЯ АВИАБИЛЕТА ===");
                _logger.LogInformation("Получена модель: {@Model}", model);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning("Ошибки валидации: {Errors}", string.Join(", ", errors));
                    return Json(new { success = false, message = "Проверьте правильность заполнения полей", errors });
                }

                var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                _logger.LogInformation("UserId: {UserId}", userId);

                // Сериализуем данные пассажиров в JSON
                var passengersJson = JsonSerializer.Serialize(model.Passengers);
                _logger.LogInformation("Пассажиры JSON: {Json}", passengersJson);

                // Генерируем номер бронирования (PNR)
                var bookingReference = GeneratePnrCode();
                var ticketNumber = GenerateTicketNumber();
                var seatNumbers = GenerateSeatNumbers(model.Passengers.Count);

                _logger.LogInformation("Сгенерировано: BookingReference={BookingRef}, TicketNumber={TicketNum}, Seats={Seats}",
                    bookingReference, ticketNumber, seatNumbers);

                // Исправляем дату рождения, если она не установлена
                foreach (var passenger in model.Passengers)
                {
                    if (passenger.DateOfBirth == default || passenger.DateOfBirth.Year < 1900)
                    {
                        passenger.DateOfBirth = new DateTime(1990, 1, 1); // Значение по умолчанию
                    }
                }

                // Создаем бронирование
                var booking = new FlightBooking
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserId = userId,
                    BookingNumber = "FLT" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999),

                    // Данные рейса туда
                    FlightId = model.Flight.FlightId,
                    Airline = model.Flight.Airline,
                    AirlineCode = model.Flight.AirlineCode,
                    AirlineLogo = model.Flight.AirlineLogo,
                    FlightNumber = model.Flight.FlightNumber,
                    DepartureCity = model.Flight.DepartureCity,
                    ArrivalCity = model.Flight.ArrivalCity,
                    DepartureAirport = model.Flight.DepartureAirport,
                    ArrivalAirport = model.Flight.ArrivalAirport,
                    DepartureDateTime = model.Flight.DepartureDateTime,
                    ArrivalDateTime = model.Flight.ArrivalDateTime,
                    Duration = model.Flight.Duration,
                    Transfers = model.Flight.Transfers,
                    Aircraft = model.Flight.Aircraft,

                    // Данные обратного рейса (если есть)
                    ReturnFlightId = model.Flight.ReturnFlightId,
                    ReturnAirline = model.Flight.ReturnAirline,
                    ReturnFlightNumber = model.Flight.ReturnFlightNumber,
                    ReturnDepartureDateTime = model.Flight.ReturnDepartureDateTime,
                    ReturnArrivalDateTime = model.Flight.ReturnArrivalDateTime,
                    ReturnDuration = model.Flight.ReturnDuration,
                    ReturnTransfers = model.Flight.ReturnTransfers,

                    // Цена и пассажиры
                    Price = model.Flight.Price,
                    Passengers = model.Passengers.Count,
                    FlightClass = model.Flight.FlightClass,
                    IsRoundTrip = model.Flight.IsRoundTrip,

                    // Багаж и услуги
                    Baggage = model.Flight.Baggage ?? "1x23кг",
                    HandLuggage = model.Flight.HandLuggage ?? "1x10кг",
                    Meal = model.Flight.Meal ?? "Включено",

                    // Контактные данные
                    ContactName = model.Contact.Name,
                    ContactEmail = model.Contact.Email,
                    ContactPhone = model.Contact.Phone,

                    // Данные пассажиров
                    PassengersJson = passengersJson,
                    SeatNumbers = seatNumbers,

                    // Статусы
                    Status = BookingStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentMethod = "Банковская карта",
                    TransactionId = "TXN" + DateTime.Now.Ticks.ToString().Substring(0, 12),
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow,
                    CancelledAt = null,

                    // Бронирование и билет
                    BookingReference = bookingReference,
                    TicketNumber = ticketNumber,

                    // Обязательные поля, которые не должны быть NULL
                    CancellationReason = "", // ВАЖНО: пустая строка вместо NULL
                    Notes = "", // ВАЖНО: пустая строка вместо NULL
                    Currency = "RUB" // ВАЖНО: значение по умолчанию
                };

                _logger.LogInformation("Создан объект бронирования: {@Booking}", booking);

                _context.FlightBookings.Add(booking);
                _logger.LogInformation("Добавлено в контекст, попытка сохранения...");

                await _context.SaveChangesAsync();
                _logger.LogInformation("Бронирование успешно сохранено в БД с ID: {BookingId}", booking.Id);

                // Сохраняем в кэш для страницы подтверждения
                var cacheKey = "FlightBooking_" + booking.Id;
                _cache.Set(cacheKey, booking, TimeSpan.FromMinutes(30));

                // Отправляем подтверждение на email
                await SendBookingConfirmationEmail(booking, model.Passengers);

                return Json(new
                {
                    success = true,
                    message = "Бронирование успешно оформлено",
                    redirectUrl = Url.Action("Confirmation", new { bookingId = booking.Id })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при бронировании авиабилета");
                _logger.LogError("Сообщение ошибки: {Message}", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Внутренняя ошибка: {InnerMessage}", ex.InnerException.Message);
                    _logger.LogError("Stack trace внутренней ошибки: {InnerStackTrace}", ex.InnerException.StackTrace);
                }
                return Json(new { success = false, message = "Произошла ошибка при бронировании: " + ex.Message });
            }
        }

        // GET: /FlightBooking/Confirmation
        [HttpGet]
        public IActionResult Confirmation(string bookingId)
        {
            if (string.IsNullOrEmpty(bookingId))
                return RedirectToAction("Index", "Flights");

            var cacheKey = "FlightBooking_" + bookingId;
            if (_cache.TryGetValue(cacheKey, out FlightBooking booking))
            {
                var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson);

                var viewModel = new FlightBookingConfirmationViewModel
                {
                    BookingId = booking.Id,
                    BookingNumber = booking.BookingNumber,
                    Airline = booking.Airline,
                    FlightNumber = booking.FlightNumber,
                    DepartureCity = booking.DepartureCity,
                    ArrivalCity = booking.ArrivalCity,
                    DepartureAirport = booking.DepartureAirport,
                    ArrivalAirport = booking.ArrivalAirport,
                    DepartureDateTime = booking.DepartureDateTime,
                    ArrivalDateTime = booking.ArrivalDateTime,
                    ReturnFlightNumber = booking.ReturnFlightNumber,
                    ReturnDepartureDateTime = booking.ReturnDepartureDateTime,
                    ReturnArrivalDateTime = booking.ReturnArrivalDateTime,
                    Passengers = booking.Passengers,
                    FlightClass = booking.FlightClass,
                    Price = booking.Price,
                    TotalPrice = booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1),
                    Currency = booking.Currency,
                    ContactName = booking.ContactName,
                    ContactEmail = booking.ContactEmail,
                    ContactPhone = booking.ContactPhone,
                    SeatNumbers = booking.SeatNumbers,
                    BookingReference = booking.BookingReference,
                    TicketNumber = booking.TicketNumber,
                    IsRoundTrip = booking.IsRoundTrip,
                    CreatedAt = booking.CreatedAt,
                    Status = GetStatusText(booking.Status)
                };
                return View(viewModel);
            }

            // Если нет в кэше, ищем в БД
            var dbBooking = _context.FlightBookings.FirstOrDefault(b => b.Id == bookingId);
            if (dbBooking != null)
            {
                var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(dbBooking.PassengersJson);

                var viewModel = new FlightBookingConfirmationViewModel
                {
                    BookingId = dbBooking.Id,
                    BookingNumber = dbBooking.BookingNumber,
                    Airline = dbBooking.Airline,
                    FlightNumber = dbBooking.FlightNumber,
                    DepartureCity = dbBooking.DepartureCity,
                    ArrivalCity = dbBooking.ArrivalCity,
                    DepartureAirport = dbBooking.DepartureAirport,
                    ArrivalAirport = dbBooking.ArrivalAirport,
                    DepartureDateTime = dbBooking.DepartureDateTime,
                    ArrivalDateTime = dbBooking.ArrivalDateTime,
                    ReturnFlightNumber = dbBooking.ReturnFlightNumber,
                    ReturnDepartureDateTime = dbBooking.ReturnDepartureDateTime,
                    ReturnArrivalDateTime = dbBooking.ReturnArrivalDateTime,
                    Passengers = dbBooking.Passengers,
                    FlightClass = dbBooking.FlightClass,
                    Price = dbBooking.Price,
                    TotalPrice = dbBooking.Price * dbBooking.Passengers * (dbBooking.IsRoundTrip ? 2 : 1),
                    Currency = dbBooking.Currency,
                    ContactName = dbBooking.ContactName,
                    ContactEmail = dbBooking.ContactEmail,
                    ContactPhone = dbBooking.ContactPhone,
                    SeatNumbers = dbBooking.SeatNumbers,
                    BookingReference = dbBooking.BookingReference,
                    TicketNumber = dbBooking.TicketNumber,
                    IsRoundTrip = dbBooking.IsRoundTrip,
                    CreatedAt = dbBooking.CreatedAt,
                    Status = GetStatusText(dbBooking.Status)
                };
                return View(viewModel);
            }

            return RedirectToAction("Index", "Flights");
        }

        // GET: /FlightBooking/MyBookings
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var bookings = await _context.FlightBookings
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        // GET: /FlightBooking/Ticket/{bookingId}
        [HttpGet]
        public async Task<IActionResult> Ticket(string bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var booking = await _context.FlightBookings
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            if (booking.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson);

            var viewModel = new FlightBookingConfirmationViewModel
            {
                BookingId = booking.Id,
                BookingNumber = booking.BookingNumber,
                Airline = booking.Airline,
                FlightNumber = booking.FlightNumber,
                DepartureCity = booking.DepartureCity,
                ArrivalCity = booking.ArrivalCity,
                DepartureAirport = booking.DepartureAirport,
                ArrivalAirport = booking.ArrivalAirport,
                DepartureDateTime = booking.DepartureDateTime,
                ArrivalDateTime = booking.ArrivalDateTime,
                ReturnFlightNumber = booking.ReturnFlightNumber,
                ReturnDepartureDateTime = booking.ReturnDepartureDateTime,
                ReturnArrivalDateTime = booking.ReturnArrivalDateTime,
                Passengers = booking.Passengers,
                FlightClass = booking.FlightClass,
                Price = booking.Price,
                TotalPrice = booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1),
                Currency = booking.Currency,
                ContactName = booking.ContactName,
                ContactEmail = booking.ContactEmail,
                ContactPhone = booking.ContactPhone,
                SeatNumbers = booking.SeatNumbers,
                BookingReference = booking.BookingReference,
                TicketNumber = booking.TicketNumber,
                IsRoundTrip = booking.IsRoundTrip,
                CreatedAt = booking.CreatedAt,
                Status = GetStatusText(booking.Status)
            };

            return View(viewModel);
        }

        // POST: /FlightBooking/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel([FromBody] CancelBookingRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");

                var booking = await _context.FlightBookings
                    .FirstOrDefaultAsync(b => b.Id == request.BookingId);

                if (booking == null)
                    return Json(new { success = false, message = "Бронирование не найдено" });

                if (booking.UserId != userId && !User.IsInRole("Admin"))
                    return Json(new { success = false, message = "Нет прав для отмены" });

                // Проверяем, можно ли отменить (за 24 часа до вылета)
                if (booking.DepartureDateTime <= DateTime.UtcNow.AddHours(24))
                {
                    return Json(new { success = false, message = "Отмена невозможна менее чем за 24 часа до вылета" });
                }

                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTime.UtcNow;
                booking.CancellationReason = request.Reason;

                await _context.SaveChangesAsync();

                // Отправляем уведомление об отмене
                await SendCancellationEmail(booking);

                return Json(new { success = true, message = "Бронирование отменено" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене бронирования");
                return Json(new { success = false, message = "Ошибка при отмене: " + ex.Message });
            }
        }

        // GET: /FlightBooking/DownloadTicket/{bookingId}
        [HttpGet]
        public async Task<IActionResult> DownloadTicket(string bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var booking = await _context.FlightBookings
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            if (booking.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            // Генерируем HTML билета
            var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson);
            var html = GenerateTicketHtml(booking, passengers);

            // Возвращаем как файл
            return File(System.Text.Encoding.UTF8.GetBytes(html), "text/html", $"ticket_{booking.TicketNumber}.html");
        }

        // Вспомогательные методы
        private string GeneratePnrCode()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GenerateTicketNumber()
        {
            var random = new Random();
            return $"TKT{DateTime.Now:yyyyMMdd}{random.Next(1000, 9999)}";
        }

        private string GenerateSeatNumbers(int count)
        {
            var seats = new List<string>();
            var random = new Random();
            var rows = new[] { "A", "B", "C", "D", "E", "F" };

            for (int i = 0; i < count; i++)
            {
                var row = random.Next(1, 35);
                var seat = rows[random.Next(rows.Length)];
                seats.Add($"{row}{seat}");
            }

            return string.Join(", ", seats);
        }

        private async Task SendBookingConfirmationEmail(FlightBooking booking, List<FlightPassengerViewModel> passengers)
        {
            var subject = $"Ваш билет на рейс {booking.FlightNumber} - Вместе В Путь";

            var departureDate = booking.DepartureDateTime.ToString("dd.MM.yyyy HH:mm");
            var arrivalDate = booking.ArrivalDateTime.ToString("dd.MM.yyyy HH:mm");

            var passengersHtml = "";
            foreach (var p in passengers)
            {
                passengersHtml += $@"
                    <tr>
                        <td>{p.LastName} {p.FirstName} {p.MiddleName}</td>
                        <td>{p.DateOfBirth:dd.MM.yyyy}</td>
                        <td>{GetDocumentTypeName(p.DocumentType)} {p.DocumentNumber}</td>
                    </tr>";
            }

            var body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <style>
                    body {{ font-family: 'Arial', sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333; }}
                    .ticket {{ border: 2px solid #0379D9; border-radius: 12px; padding: 20px; background: #f8fafc; }}
                    .header {{ background: linear-gradient(135deg, #0379D9, #40B624); color: white; padding: 20px; border-radius: 12px 12px 0 0; margin: -20px -20px 20px -20px; }}
                    .header h2 {{ margin: 0; font-size: 24px; }}
                    .airline {{ font-size: 24px; font-weight: bold; text-align: center; margin: 20px 0; color: #0379D9; }}
                    .flight {{ font-size: 20px; font-weight: bold; text-align: center; color: #334155; margin: 10px 0; }}
                    .route {{ display: flex; justify-content: space-between; align-items: center; margin: 30px 0; }}
                    .city {{ text-align: center; }}
                    .city-name {{ font-size: 18px; font-weight: bold; }}
                    .airport {{ color: #64748b; }}
                    .time {{ font-size: 16px; color: #0379D9; font-weight: bold; margin-top: 5px; }}
                    .arrow {{ color: #94a3b8; font-size: 24px; }}
                    .info {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin: 20px 0; }}
                    .info-item {{ border-bottom: 1px solid #e2e8f0; padding: 10px 0; }}
                    .info-item .label {{ color: #64748b; font-size: 12px; }}
                    .info-item .value {{ font-size: 16px; font-weight: bold; color: #334155; }}
                    table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
                    th {{ background: #f1f5f9; color: #334155; padding: 10px; text-align: left; }}
                    td {{ padding: 10px; border-bottom: 1px solid #e2e8f0; }}
                    .price {{ background: #e8f4fe; padding: 15px; border-radius: 8px; text-align: center; margin: 20px 0; }}
                    .price .total {{ font-size: 24px; font-weight: bold; color: #0379D9; }}
                    .qr {{ text-align: center; margin: 30px 0; }}
                    .qr-placeholder {{ width: 150px; height: 150px; background: #f1f5f9; border: 2px dashed #0379D9; border-radius: 12px; margin: 0 auto; display: flex; align-items: center; justify-content: center; color: #0379D9; }}
                    .footer {{ text-align: center; margin-top: 30px; color: #94a3b8; font-size: 12px; }}
                </style>
            </head>
            <body>
                <div class='ticket'>
                    <div class='header'>
                        <h2>Электронный билет</h2>
                        <p>Номер бронирования: {booking.BookingReference}</p>
                        <p>Номер билета: {booking.TicketNumber}</p>
                    </div>

                    <div class='airline'>
                        {booking.Airline}
                    </div>

                    <div class='flight'>
                        Рейс {booking.FlightNumber}
                    </div>

                    <div class='route'>
                        <div class='city'>
                            <div class='city-name'>{booking.DepartureCity}</div>
                            <div class='airport'>{booking.DepartureAirport}</div>
                            <div class='time'>{booking.DepartureDateTime:HH:mm}</div>
                            <div class='date'>{booking.DepartureDateTime:dd.MM.yyyy}</div>
                        </div>
                        <div class='arrow'>
                            <i class='fas fa-plane'></i> ✈
                        </div>
                        <div class='city'>
                            <div class='city-name'>{booking.ArrivalCity}</div>
                            <div class='airport'>{booking.ArrivalAirport}</div>
                            <div class='time'>{booking.ArrivalDateTime:HH:mm}</div>
                            <div class='date'>{booking.ArrivalDateTime:dd.MM.yyyy}</div>
                        </div>
                    </div>";

            if (booking.IsRoundTrip && booking.ReturnFlightNumber != null)
            {
                body += $@"
                    <div style='margin: 30px 0; border-top: 2px dashed #e2e8f0; padding-top: 30px;'>
                        <div class='flight'>Обратный рейс {booking.ReturnFlightNumber}</div>
                        <div class='route'>
                            <div class='city'>
                                <div class='city-name'>{booking.ArrivalCity}</div>
                                <div class='airport'>{booking.ArrivalAirport}</div>
                                <div class='time'>{booking.ReturnDepartureDateTime:HH:mm}</div>
                                <div class='date'>{booking.ReturnDepartureDateTime:dd.MM.yyyy}</div>
                            </div>
                            <div class='arrow'>✈</div>
                            <div class='city'>
                                <div class='city-name'>{booking.DepartureCity}</div>
                                <div class='airport'>{booking.DepartureAirport}</div>
                                <div class='time'>{booking.ReturnArrivalDateTime:HH:mm}</div>
                                <div class='date'>{booking.ReturnArrivalDateTime:dd.MM.yyyy}</div>
                            </div>
                        </div>
                    </div>";
            }

            body += $@"
                    <h3>Пассажиры</h3>
                    <table>
                        <thead>
                            <tr>
                                <th>ФИО</th>
                                <th>Дата рождения</th>
                                <th>Документ</th>
                            </tr>
                        </thead>
                        <tbody>
                            {passengersHtml}
                        </tbody>
                    </table>

                    <div class='info'>
                        <div class='info-item'>
                            <div class='label'>Класс</div>
                            <div class='value'>{GetClassName(booking.FlightClass)}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Багаж</div>
                            <div class='value'>{booking.Baggage}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Ручная кладь</div>
                            <div class='value'>{booking.HandLuggage}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Питание</div>
                            <div class='value'>{booking.Meal}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Места</div>
                            <div class='value'>{booking.SeatNumbers}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Контакт</div>
                            <div class='value'>{booking.ContactName}, {booking.ContactPhone}</div>
                        </div>
                    </div>

                    <div class='price'>
                        <p>Цена за билет: {booking.Price:N0} {booking.Currency}</p>
                        <p>Количество пассажиров: {booking.Passengers}</p>
                        <p class='total'>Итого: {booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1):N0} {booking.Currency}</p>
                    </div>

                    <div class='qr'>
                        <div class='qr-placeholder'>
                            <i class='fas fa-qrcode fa-4x'></i>
                        </div>
                        <p style='color: #64748b; margin-top: 10px;'>QR-код для посадки</p>
                    </div>

                    <div style='background: #e2e8f0; padding: 15px; border-radius: 8px; margin-top: 20px;'>
                        <p style='margin: 0; color: #334155;'><strong>Важно!</strong> Для посадки на рейс необходимо предъявить документ, указанный при оформлении, и данный электронный билет (можно на экране телефона).</p>
                        <p style='margin: 10px 0 0 0;'><strong>Регистрация на рейс открывается за 24 часа до вылета.</strong></p>
                    </div>

                    <div class='footer'>
                        <p>Спасибо, что пользуетесь сервисом <strong>Вместе В Путь</strong></p>
                        <p>© {DateTime.Now.Year} Все права защищены</p>
                    </div>
                </div>
            </body>
            </html>";

            await _emailService.SendAsync(booking.ContactEmail, subject, body);
        }

        private async Task SendCancellationEmail(FlightBooking booking)
        {
            var subject = $"Отмена бронирования рейса {booking.FlightNumber} - Вместе В Путь";

            var body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <style>
                    body {{ font-family: 'Arial', sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333; }}
                    .cancellation {{ border: 2px solid #dc3545; border-radius: 12px; padding: 20px; background: #f8fafc; }}
                    .header {{ background: #dc3545; color: white; padding: 20px; border-radius: 12px 12px 0 0; margin: -20px -20px 20px -20px; }}
                </style>
            </head>
            <body>
                <div class='cancellation'>
                    <div class='header'>
                        <h2>Бронирование отменено</h2>
                        <p>Номер бронирования: {booking.BookingReference}</p>
                    </div>

                    <p><strong>Авиакомпания:</strong> {booking.Airline}</p>
                    <p><strong>Рейс:</strong> {booking.FlightNumber}</p>
                    <p><strong>Маршрут:</strong> {booking.DepartureCity} → {booking.ArrivalCity}</p>
                    <p><strong>Дата вылета:</strong> {booking.DepartureDateTime:dd.MM.yyyy HH:mm}</p>
                    <p><strong>Причина отмены:</strong> {booking.CancellationReason ?? "Не указана"}</p>

                    <p>Средства будут возвращены на карту в течение 3-7 рабочих дней.</p>
                </div>
            </body>
            </html>";

            await _emailService.SendAsync(booking.ContactEmail, subject, body);
        }

        private string GenerateTicketHtml(FlightBooking booking, List<FlightPassengerViewModel> passengers)
        {
            // Аналогично телу письма, но для файла
            return SendBookingConfirmationEmail(booking, passengers).ToString();
        }

        private string GetStatusText(BookingStatus status)
        {
            return status switch
            {
                BookingStatus.Pending => "Ожидает подтверждения",
                BookingStatus.Confirmed => "Подтверждено",
                BookingStatus.Cancelled => "Отменено",
                BookingStatus.Completed => "Завершено",
                _ => "Неизвестно"
            };
        }

        private string GetClassName(string flightClass)
        {
            return flightClass.ToLower() switch
            {
                "economy" => "Эконом",
                "business" => "Бизнес",
                "first" => "Первый",
                _ => flightClass
            };
        }

        private string GetDocumentTypeName(string type)
        {
            return type switch
            {
                "passport" => "Паспорт РФ",
                "foreign_passport" => "Загранпаспорт",
                "birth_certificate" => "Свидетельство о рождении",
                "military_id" => "Военный билет",
                _ => type
            };
        }
    }
}