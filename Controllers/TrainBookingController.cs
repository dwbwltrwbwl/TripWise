// Controllers/TrainBookingController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.ViewModels;
using TripWise.Services;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace TripWise.Controllers
{
    public class TrainBookingController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<TrainBookingController> _logger;
        private readonly IMemoryCache _cache;

        public TrainBookingController(
            TripWiseContext context,
            EmailService emailService,
            ILogger<TrainBookingController> logger,
            IMemoryCache memoryCache)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cache = memoryCache;
        }

        // GET: /TrainBooking/Book
        [HttpGet]
        public IActionResult Book([FromQuery] TrainBookingViewModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.TrainNumber))
            {
                return RedirectToAction("Index", "Railway");
            }

            // Создаем ViewModel для формы
            var viewModel = new CompleteBookingViewModel
            {
                TrainInfo = model,
                Passenger = new PassengerInfoViewModel(),
                Contact = new ContactInfoViewModel()
            };

            // Если пользователь авторизован, подставляем его данные
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var user = _context.Users.Find(userId.Value);
                if (user != null)
                {
                    viewModel.Contact.Email = user.Email;

                    // Подставляем ФИО из профиля
                    viewModel.Passenger.LastName = user.LastName ?? "";
                    viewModel.Passenger.FirstName = user.FirstName ?? "";
                    viewModel.Passenger.MiddleName = user.MiddleName;
                }
            }

            return View(viewModel);
        }

        // POST: /TrainBooking/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment([FromBody] CompleteBookingViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new { success = false, message = "Проверьте правильность заполнения полей", errors });
                }

                var userId = HttpContext.Session.GetInt32("UserId");

                // Генерируем номер заказа
                var orderId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

                // Создаем заказ
                var trainOrder = new TrainOrder
                {
                    Id = orderId,
                    UserId = userId ?? 0,
                    OrderNumber = "RZD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999),
                    TrainNumber = model.TrainInfo.TrainNumber,
                    ReturnTrainNumber = model.TrainInfo.ReturnTrainNumber,
                    DepartureStationId = model.TrainInfo.DepartureStationId,
                    DepartureStationName = model.TrainInfo.DepartureStationName,
                    ArrivalStationId = model.TrainInfo.ArrivalStationId,
                    ArrivalStationName = model.TrainInfo.ArrivalStationName,
                    DepartureDateTime = model.TrainInfo.DepartureDateTime,
                    ArrivalDateTime = model.TrainInfo.ArrivalDateTime,
                    ReturnDepartureDateTime = model.TrainInfo.ReturnDepartureDateTime,
                    ReturnArrivalDateTime = model.TrainInfo.ReturnArrivalDateTime,
                    TotalPrice = model.TotalPrice,
                    Passengers = model.TrainInfo.Passengers,
                    CarType = model.TrainInfo.CarType,
                    CarClass = model.TrainInfo.CarClass,
                    ContactEmail = model.Contact.Email,
                    ContactPhone = model.Contact.Phone,
                    PassengerFullName = $"{model.Passenger.LastName} {model.Passenger.FirstName} {model.Passenger.MiddleName}".Trim(),
                    PassengerDocumentType = model.Passenger.DocumentType,
                    PassengerDocumentNumber = model.Passenger.DocumentNumber,
                    Status = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    IsRoundTrip = model.TrainInfo.IsRoundTrip,
                    Duration = model.TrainInfo.Duration,
                    ReturnDuration = model.TrainInfo.ReturnDuration
                };

                _context.TrainOrders.Add(trainOrder);
                await _context.SaveChangesAsync();

                // Имитация обработки платежа
                await Task.Delay(2000);

                // Обновляем статус заказа
                trainOrder.PaymentStatus = PaymentStatus.Paid;
                trainOrder.Status = OrderStatus.Confirmed;
                trainOrder.ConfirmedAt = DateTime.UtcNow;
                trainOrder.TransactionId = "TXN" + DateTime.Now.Ticks.ToString().Substring(0, 12);
                trainOrder.BookingReference = "BR" + new Random().Next(100000, 999999).ToString();
                trainOrder.TicketNumber = "TKT" + DateTime.Now.ToString("yyyyMMdd") + new Random().Next(1000, 9999);

                // Генерируем номера мест
                var seats = GenerateSeatNumbers(model.TrainInfo.Passengers);
                trainOrder.SeatNumbers = string.Join(", ", seats);
                trainOrder.CarNumber = new Random().Next(1, 15).ToString();

                await _context.SaveChangesAsync();

                // Сохраняем в кэш для страницы подтверждения
                var cacheKey = "TrainOrder_" + orderId;
                _cache.Set(cacheKey, trainOrder, TimeSpan.FromMinutes(30));

                // Отправляем билет на email
                await SendTicketEmail(trainOrder, model.Passenger);

                return Json(new
                {
                    success = true,
                    message = "Билет успешно оплачен",
                    redirectUrl = Url.Action("Confirmation", new { orderId = orderId })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке платежа");
                return Json(new { success = false, message = "Произошла ошибка при оплате: " + ex.Message });
            }
        }

        // GET: /TrainBooking/Confirmation
        [HttpGet]
        public IActionResult Confirmation(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return RedirectToAction("Index", "Railway");

            var cacheKey = "TrainOrder_" + orderId;
            if (_cache.TryGetValue(cacheKey, out TrainOrder order))
            {
                return View(order);
            }

            // Если нет в кэше, ищем в БД
            var dbOrder = _context.TrainOrders.FirstOrDefault(o => o.Id == orderId);
            if (dbOrder != null)
            {
                return View(dbOrder);
            }

            return RedirectToAction("Index", "Railway");
        }

        // GET: /TrainBooking/MyTickets
        [HttpGet]
        public async Task<IActionResult> MyTickets()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var orders = await _context.TrainOrders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: /TrainBooking/Ticket/{orderId}
        [HttpGet]
        public async Task<IActionResult> Ticket(string orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var order = await _context.TrainOrders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound();

            // Проверяем, что это заказ текущего пользователя или админ
            if (order.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            return View(order);
        }

        // GET: /TrainBooking/DownloadTicket/{orderId}
        [HttpGet]
        public async Task<IActionResult> DownloadTicket(string orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var order = await _context.TrainOrders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound();

            if (order.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            // Генерируем PDF билета
            var pdfBytes = GenerateTicketPdf(order);

            return File(pdfBytes, "application/pdf", $"ticket_{order.OrderNumber}.pdf");
        }

        // Вспомогательные методы
        private List<string> GenerateSeatNumbers(int count)
        {
            var seats = new List<string>();
            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var car = random.Next(1, 15);
                var seat = random.Next(1, 50);
                seats.Add($"{car} вагон, {seat} место");
            }

            return seats;
        }

        private async Task SendTicketEmail(TrainOrder order, PassengerInfoViewModel passenger)
        {
            var subject = $"Ваш билет на поезд {order.TrainNumber} - Вместе В Путь";

            // Форматируем даты
            var departureDate = order.DepartureDateTime.ToString("dd.MM.yyyy HH:mm");
            var arrivalDate = order.ArrivalDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "—";

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
                    .route {{ font-size: 28px; font-weight: bold; text-align: center; margin: 20px 0; color: #0379D9; }}
                    .info {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin: 20px 0; }}
                    .info-item {{ border-bottom: 1px solid #e2e8f0; padding: 10px 0; }}
                    .info-item .label {{ color: #64748b; font-size: 12px; }}
                    .info-item .value {{ font-size: 16px; font-weight: bold; color: #334155; }}
                    .qr {{ text-align: center; margin: 30px 0; }}
                    .qr-placeholder {{ width: 150px; height: 150px; background: #f1f5f9; border: 2px dashed #0379D9; border-radius: 12px; margin: 0 auto; display: flex; align-items: center; justify-content: center; color: #0379D9; }}
                    .footer {{ text-align: center; margin-top: 30px; color: #94a3b8; font-size: 12px; }}
                </style>
            </head>
            <body>
                <div class='ticket'>
                    <div class='header'>
                        <h2>Электронный билет</h2>
                        <p>Заказ № {order.OrderNumber}</p>
                    </div>

                    <div class='route'>
                        {order.DepartureStationName} → {order.ArrivalStationName}
                    </div>

                    <div class='info'>
                        <div class='info-item'>
                            <div class='label'>Поезд</div>
                            <div class='value'>№ {order.TrainNumber} {GetTrainType(order.TrainNumber)}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Тип вагона</div>
                            <div class='value'>{order.CarType} ({order.CarClass})</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Отправление</div>
                            <div class='value'>{departureDate}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Прибытие</div>
                            <div class='value'>{arrivalDate}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Время в пути</div>
                            <div class='value'>{FormatDuration(order.Duration)}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Пассажир</div>
                            <div class='value'>{passenger.LastName} {passenger.FirstName} {passenger.MiddleName}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Документ</div>
                            <div class='value'>{GetDocumentTypeName(passenger.DocumentType)} {passenger.DocumentNumber}</div>
                        </div>
                        <div class='info-item'>
                            <div class='label'>Места</div>
                            <div class='value'>{order.SeatNumbers}</div>
                        </div>
                    </div>

                    <div class='qr'>
                        <div class='qr-placeholder'>
                            <i class='fas fa-qrcode fa-4x'></i>
                        </div>
                        <p style='color: #64748b; margin-top: 10px;'>QR-код для посадки</p>
                    </div>

                    <div style='background: #e2e8f0; padding: 15px; border-radius: 8px; margin-top: 20px;'>
                        <p style='margin: 0; color: #334155;'><strong>Важно!</strong> Для посадки необходимо предъявить документ, указанный в билете, и данный электронный билет (можно на экране телефона).</p>
                    </div>

                    <div class='footer'>
                        <p>Спасибо, что пользуетесь сервисом <strong>Вместе В Путь</strong></p>
                        <p>© {DateTime.Now.Year} Все права защищены</p>
                    </div>
                </div>
            </body>
            </html>";

            await _emailService.SendAsync(order.ContactEmail, subject, body);
        }

        private string GetTrainType(string trainNumber)
        {
            if (trainNumber.StartsWith("0") || trainNumber.StartsWith("1") || trainNumber.StartsWith("2"))
                return "Фирменный";
            if (trainNumber.StartsWith("3") || trainNumber.StartsWith("4"))
                return "Скоростной";
            if (trainNumber.StartsWith("7") || trainNumber.StartsWith("8"))
                return "Пригородный";
            return "Пассажирский";
        }

        private string FormatDuration(int minutes)
        {
            var hours = minutes / 60;
            var mins = minutes % 60;
            return $"{hours} ч {mins} мин";
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

        private byte[] GenerateTicketPdf(TrainOrder order)
        {
            // Здесь должна быть генерация PDF
            // Пока возвращаем пустой массив
            return new byte[0];
        }
    }
}