using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System.Security.Claims;
using Microsoft.Data.SqlClient;

namespace TripWise.Controllers
{
    public class BudgetController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<BudgetController> _logger;

        public BudgetController(TripWiseContext context, ILogger<BudgetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        // GET: /Budget/GetSummary
        [HttpGet]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<BudgetSummaryDto>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("GetSummary для пользователя {UserId}", userId);

                // Получаем все поездки пользователя
                var userTripIds = await _context.TripParticipants
                    .Where(tp => tp.IdUser == userId)
                    .Select(tp => tp.IdTrip)
                    .ToListAsync();

                // Получаем все расходы пользователя (где он участник)
                var expenses = await _context.Expenses
                    .Include(e => e.IdExpenseCategoryNavigation)
                    .Include(e => e.IdTripNavigation)
                    .Include(e => e.PaidBy)
                    .Include(e => e.ExpenseShares)
                        .ThenInclude(es => es.IdUserNavigation)
                    .Where(e => userTripIds.Contains(e.IdTrip))
                    .OrderByDescending(e => e.ExpenseDate)
                    .ToListAsync();

                // Получаем категории расходов
                var categories = await _context.ExpenseCategories
                    .Select(c => new
                    {
                        c.IdExpenseCategory,
                        c.ExpenseCategoryName
                    })
                    .ToListAsync();

                // Формируем DTO для категорий - ТЕПЕРЬ ВСЕ ВЫЧИСЛЕНИЯ В ПАМЯТИ
                var categoryDtos = categories.Select(c =>
                {
                    // Вычисляем сумму расходов по этой категории (в памяти)
                    var spent = expenses
                        .Where(e => e.IdExpenseCategory == c.IdExpenseCategory)
                        .Sum(e => e.Amount);

                    return new BudgetCategoryDto
                    {
                        Id = c.IdExpenseCategory,
                        Name = c.ExpenseCategoryName ?? "Без категории",
                        Budget = 0, // Пока без бюджета
                        Spent = spent,
                        Color = GetCategoryColor(c.ExpenseCategoryName ?? ""),
                        ExpenseCount = expenses.Count(e => e.IdExpenseCategory == c.IdExpenseCategory)
                    };
                }).ToList();

                // Добавляем категорию "Другое" для расходов без категории
                var uncategorizedExpenses = expenses.Where(e => e.IdExpenseCategory == null);
                if (uncategorizedExpenses.Any())
                {
                    categoryDtos.Add(new BudgetCategoryDto
                    {
                        Id = 0,
                        Name = "Другое",
                        Budget = 0,
                        Spent = uncategorizedExpenses.Sum(e => e.Amount),
                        Color = "#6c757d",
                        ExpenseCount = uncategorizedExpenses.Count()
                    });
                }

                // Получаем информацию о поездках - ИСПРАВЛЕНО
                var trips = new List<TripBudgetDto>();
                foreach (var tripId in userTripIds)
                {
                    var trip = await _context.Trips
                        .FirstOrDefaultAsync(t => t.IdTrip == tripId);

                    if (trip != null)
                    {
                        var tripExpenses = expenses.Where(e => e.IdTrip == tripId).ToList();

                        trips.Add(new TripBudgetDto
                        {
                            Id = trip.IdTrip,
                            Title = trip.Title ?? "Без названия",
                            StartDate = trip.StartDate,
                            EndDate = trip.EndDate,
                            TotalBudget = trip.TotalBudget,
                            TotalSpent = tripExpenses.Sum(e => e.Amount),
                            ParticipantCount = await _context.TripParticipants
                                .CountAsync(tp => tp.IdTrip == tripId),
                            Participants = await _context.TripParticipants
                                .Where(tp => tp.IdTrip == tripId)
                                .Select(tp => tp.IdUserNavigation.LastName + " " + tp.IdUserNavigation.FirstName)
                                .ToListAsync()
                        });
                    }
                }

                // Формируем последние расходы
                var recentExpenses = expenses.Take(10).Select(e => new RecentExpenseDto
                {
                    Id = e.IdExpense,
                    Title = e.Title ?? "Без названия",
                    Amount = e.Amount,
                    ExpenseDate = e.ExpenseDate,
                    CategoryName = e.IdExpenseCategoryNavigation?.ExpenseCategoryName ?? "Другое",
                    TripName = e.IdTripNavigation?.Title ?? "Поездка",
                    TripId = e.IdTrip,
                    PaidByName = e.PaidBy != null
                        ? $"{e.PaidBy.LastName} {e.PaidBy.FirstName}".Trim()
                        : "Неизвестно",
                    PaidById = e.PaidById ?? 0,
                    Shares = e.ExpenseShares.Select(es => new ExpenseShareDto
                    {
                        UserId = es.IdUser,
                        UserName = es.IdUserNavigation != null
                            ? $"{es.IdUserNavigation.LastName} {es.IdUserNavigation.FirstName}".Trim()
                            : "Неизвестно",
                        Amount = es.ShareAmount,
                        IsPaid = es.IsPaid
                    }).ToList()
                }).ToList();

                var summary = new BudgetSummaryDto
                {
                    TotalBudget = trips.Sum(t => t.TotalBudget),
                    TotalSpent = expenses.Sum(e => e.Amount),
                    TripCount = trips.Count,
                    Categories = categoryDtos.OrderByDescending(c => c.Spent).ToList(),
                    RecentExpenses = recentExpenses,
                    Trips = trips
                };

                return Json(new ApiResponse<BudgetSummaryDto>
                {
                    Success = true,
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сводки бюджета");
                return Json(new ApiResponse<BudgetSummaryDto>
                {
                    Success = false,
                    Message = "Ошибка при загрузке данных: " + ex.Message
                });
            }
        }

        // POST: /Budget/AddExpense
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExpense([FromBody] CreateExpenseRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("AddExpense: userId={UserId}, title={Title}, amount={Amount}",
                    userId, request.Title, request.Amount);

                // Проверяем, является ли пользователь участником поездки
                var isParticipant = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == userId);

                if (!isParticipant)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Вы не являетесь участником этой поездки"
                    });
                }

                // Создаем расход
                var expense = new Expense
                {
                    Title = request.Title,
                    Amount = request.Amount,
                    IdExpenseCategory = request.CategoryId,
                    ExpenseDate = request.ExpenseDate.ToUniversalTime(),
                    CreatedAt = DateTime.UtcNow,
                    IdTrip = request.TripId,
                    PaidById = userId.Value,
                    IdPoint = request.PointId
                };

                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();

                // Если указаны участники для разделения расхода
                if (request.SharedWith != null && request.SharedWith.Any())
                {
                    // Добавляем самого себя в список для разделения
                    var allParticipants = request.SharedWith.Distinct().ToList();
                    if (!allParticipants.Contains(userId.Value))
                    {
                        allParticipants.Add(userId.Value);
                    }

                    var shareAmount = request.Amount / allParticipants.Count;

                    foreach (var participantId in allParticipants)
                    {
                        var share = new ExpenseShare
                        {
                            IdExpense = expense.IdExpense,
                            IdUser = participantId,
                            ShareAmount = shareAmount,
                            IsPaid = participantId == userId.Value // Тот, кто заплатил, уже оплатил свою долю
                        };
                        _context.ExpenseShares.Add(share);
                    }

                    await _context.SaveChangesAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Расход успешно добавлен",
                    Data = new { expenseId = expense.IdExpense }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении расхода");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при добавлении расхода: " + ex.Message
                });
            }
        }

        // POST: /Budget/AddCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Проверяем, существует ли уже такая категория
                var existingCategory = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.ExpenseCategoryName == request.Name);

                if (existingCategory != null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Категория с таким названием уже существует"
                    });
                }

                var category = new ExpenseCategory
                {
                    ExpenseCategoryName = request.Name
                };

                _context.ExpenseCategories.Add(category);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Категория успешно добавлена",
                    Data = new { categoryId = category.IdExpenseCategory }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении категории");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при добавлении категории: " + ex.Message
                });
            }
        }

        // POST: /Budget/MarkShareAsPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkShareAsPaid([FromBody] UpdateExpenseShareRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var share = await _context.ExpenseShares
                    .FirstOrDefaultAsync(es => es.IdExpense == request.ExpenseId && es.IdUser == request.UserId);

                if (share == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Доля не найдена"
                    });
                }

                share.IsPaid = request.IsPaid;
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = request.IsPaid ? "Доля отмечена как оплаченная" : "Доля отмечена как неоплаченная"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении статуса оплаты");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении статуса: " + ex.Message
                });
            }
        }

        // GET: /Budget/GetTripParticipants?tripId=5
        [HttpGet]
        public async Task<IActionResult> GetTripParticipants(int tripId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var participants = await _context.TripParticipants
                    .Include(tp => tp.IdUserNavigation)
                    .Where(tp => tp.IdTrip == tripId)
                    .Select(tp => new
                    {
                        tp.IdUser,
                        FullName = tp.IdUserNavigation.LastName + " " + tp.IdUserNavigation.FirstName,
                        tp.IdUserNavigation.AvatarPath
                    })
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = participants
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении участников поездки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке участников: " + ex.Message
                });
            }
        }

        private string GetCategoryColor(string categoryName)
        {
            var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Транспорт"] = "#0379D9",
                ["Проживание"] = "#40B624",
                ["Питание"] = "#FF6B6B",
                ["Развлечения"] = "#FFC107",
                ["Шоппинг"] = "#6F42C1",
                ["Экскурсии"] = "#17A2B8",
                ["Другое"] = "#6c757d"
            };

            return colors.ContainsKey(categoryName) ? colors[categoryName] : "#6c757d";
        }
        // GET: /Budget/GetExpensesForChat?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetExpensesForChat(int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Получаем поездку по chatId
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdChat == chatId && c.Type == "trip");

                if (chat == null || !chat.IdTrip.HasValue)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Data = new List<ExpenseWithChatDto>()
                    });
                }

                // Получаем расходы для этой поездки
                var expenses = await _context.Expenses
                    .Include(e => e.IdExpenseCategoryNavigation)
                    .Include(e => e.IdTripNavigation)
                    .Include(e => e.PaidBy)
                    .Include(e => e.ExpenseShares)
                        .ThenInclude(es => es.IdUserNavigation)
                    .Where(e => e.IdTrip == chat.IdTrip)
                    .OrderByDescending(e => e.ExpenseDate)
                    .Take(20)
                    .Select(e => new ExpenseWithChatDto
                    {
                        Id = e.IdExpense,
                        Title = e.Title ?? "Без названия",
                        Amount = e.Amount,
                        CategoryName = e.IdExpenseCategoryNavigation != null
                            ? e.IdExpenseCategoryNavigation.ExpenseCategoryName ?? "Другое"
                            : "Другое",
                        TripName = e.IdTripNavigation != null ? e.IdTripNavigation.Title ?? "Поездка" : "Поездка",
                        TripId = e.IdTrip,
                        PaidByName = e.PaidBy != null
                            ? $"{e.PaidBy.LastName} {e.PaidBy.FirstName}".Trim()
                            : "Неизвестно",
                        ChatId = chatId,
                        Shares = e.ExpenseShares.Select(es => new ExpenseShareDto
                        {
                            UserId = es.IdUser,
                            UserName = es.IdUserNavigation != null
                                ? $"{es.IdUserNavigation.LastName} {es.IdUserNavigation.FirstName}".Trim()
                                : "Неизвестно",
                            Amount = es.ShareAmount,
                            IsPaid = es.IsPaid
                        }).ToList()
                    })
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = expenses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении расходов для чата {ChatId}", chatId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке расходов: " + ex.Message
                });
            }
        }

        // GET: /Budget/GetDebtsForChat?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetDebtsForChat(int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Получаем поездку по chatId
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdChat == chatId && c.Type == "trip");

                if (chat == null || !chat.IdTrip.HasValue)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Data = new List<DebtReminderDto>()
                    });
                }

                var tripId = chat.IdTrip.Value;

                // Получаем все расходы для этой поездки
                var expenses = await _context.Expenses
                    .Include(e => e.ExpenseShares)
                    .Where(e => e.IdTrip == tripId)
                    .ToListAsync();

                // Получаем всех участников поездки
                var participants = await _context.TripParticipants
                    .Include(tp => tp.IdUserNavigation)
                    .Where(tp => tp.IdTrip == tripId)
                    .ToDictionaryAsync(tp => tp.IdUser, tp => $"{tp.IdUserNavigation.LastName} {tp.IdUserNavigation.FirstName}".Trim());

                // Рассчитываем балансы
                var balances = new Dictionary<int, decimal>();
                var expenseGroups = new Dictionary<int, List<int>>(); // Для группировки долгов по расходам

                foreach (var expense in expenses)
                {
                    var shares = await _context.ExpenseShares
                        .Where(es => es.IdExpense == expense.IdExpense)
                        .ToListAsync();

                    foreach (var share in shares)
                    {
                        if (!balances.ContainsKey(share.IdUser))
                            balances[share.IdUser] = 0;

                        if (!expenseGroups.ContainsKey(share.IdUser))
                            expenseGroups[share.IdUser] = new List<int>();

                        if (share.IdUser == expense.PaidById)
                        {
                            // Тот, кто заплатил, должен получить деньги
                            balances[share.IdUser] += expense.Amount - share.ShareAmount;
                            expenseGroups[share.IdUser].Add(expense.IdExpense);
                        }
                        else
                        {
                            // Остальные должны
                            balances[share.IdUser] -= share.ShareAmount;
                            expenseGroups[share.IdUser].Add(expense.IdExpense);
                        }
                    }
                }

                // Формируем список долгов
                var debts = new List<DebtReminderDto>();
                var users = balances.Keys.ToList();

                for (int i = 0; i < users.Count; i++)
                {
                    for (int j = i + 1; j < users.Count; j++)
                    {
                        var user1 = users[i];
                        var user2 = users[j];

                        if (balances[user1] > 0 && balances[user2] < 0)
                        {
                            var amount = Math.Min(balances[user1], -balances[user2]);
                            if (amount > 0.01m)
                            {
                                debts.Add(new DebtReminderDto
                                {
                                    DebtorId = user2,
                                    DebtorName = participants.ContainsKey(user2) ? participants[user2] : "Неизвестно",
                                    CreditorId = user1,
                                    CreditorName = participants.ContainsKey(user1) ? participants[user1] : "Неизвестно",
                                    Amount = amount,
                                    TripId = tripId,
                                    TripName = chat.Name?.Replace("Чат: ", "") ?? "Поездка",
                                    ChatId = chatId,
                                    ExpenseIds = expenseGroups[user2].Intersect(expenseGroups[user1]).ToList()
                                });
                            }
                        }
                        else if (balances[user1] < 0 && balances[user2] > 0)
                        {
                            var amount = Math.Min(-balances[user1], balances[user2]);
                            if (amount > 0.01m)
                            {
                                debts.Add(new DebtReminderDto
                                {
                                    DebtorId = user1,
                                    DebtorName = participants.ContainsKey(user1) ? participants[user1] : "Неизвестно",
                                    CreditorId = user2,
                                    CreditorName = participants.ContainsKey(user2) ? participants[user2] : "Неизвестно",
                                    Amount = amount,
                                    TripId = tripId,
                                    TripName = chat.Name?.Replace("Чат: ", "") ?? "Поездка",
                                    ChatId = chatId,
                                    ExpenseIds = expenseGroups[user1].Intersect(expenseGroups[user2]).ToList()
                                });
                            }
                        }
                    }
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = debts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении долгов для чата {ChatId}", chatId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке долгов: " + ex.Message
                });
            }
        }

        // POST: /Budget/SendDebtReminder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDebtReminder([FromBody] SendDebtReminderRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Проверяем, что пользователь - либо должник, либо кредитор
                if (userId != request.FromUserId && userId != request.ToUserId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Вы не можете отправлять напоминания по этому долгу"
                    });
                }

                // Получаем чат поездки
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.TripId && c.Type == "trip");

                if (chat == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Чат поездки не найден"
                    });
                }

                // Определяем текст сообщения в зависимости от того, кто отправляет
                string messageText;
                if (request.FromUserId == userId)
                {
                    // Я должен другому
                    messageText = $"🔔 Напоминание о долге: Я должен {request.Amount} ₽";
                }
                else if (request.ToUserId == userId)
                {
                    // Мне должны
                    messageText = $"🔔 Напоминание о долге: Мне должны {request.Amount} ₽";
                }
                else
                {
                    // Кто-то другой напоминает кому-то
                    messageText = $"🔔 Напоминание о долге: {request.Amount} ₽";
                }

                // Создаем сообщение-напоминание в чате
                var message = new ChatMessage
                {
                    Message = messageText,
                    SentAt = DateTime.UtcNow,
                    SenderId = userId.Value, // Используем SenderId вместо IdUser
                    ChatId = chat.IdChat, // Используем ChatId вместо IdChat
                    AttachmentType = "reminder",
                    AttachmentsJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "debt_reminder",
                        fromUserId = request.FromUserId,
                        toUserId = request.ToUserId,
                        amount = request.Amount,
                        tripId = request.TripId
                    })
                };

                _context.ChatMessages.Add(message);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Напоминание отправлено в чат"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке напоминания о долге");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отправке напоминания: " + ex.Message
                });
            }
        }

        // POST: /Budget/CreateExpenseFromChat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExpenseFromChat([FromBody] CreateExpenseRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Создаем расход (как в обычном AddExpense)
                var expense = new Expense
                {
                    Title = request.Title,
                    Amount = request.Amount,
                    IdExpenseCategory = request.CategoryId,
                    ExpenseDate = request.ExpenseDate.ToUniversalTime(),
                    CreatedAt = DateTime.UtcNow,
                    IdTrip = request.TripId,
                    PaidById = userId.Value,
                    IdPoint = request.PointId
                };

                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();

                // Добавляем доли
                if (request.SharedWith != null && request.SharedWith.Any())
                {
                    var allParticipants = request.SharedWith.Distinct().ToList();
                    if (!allParticipants.Contains(userId.Value))
                    {
                        allParticipants.Add(userId.Value);
                    }

                    var shareAmount = request.Amount / allParticipants.Count;

                    foreach (var participantId in allParticipants)
                    {
                        var share = new ExpenseShare
                        {
                            IdExpense = expense.IdExpense,
                            IdUser = participantId,
                            ShareAmount = shareAmount,
                            IsPaid = participantId == userId.Value
                        };
                        _context.ExpenseShares.Add(share);
                    }

                    await _context.SaveChangesAsync();
                }

                // Отправляем уведомление в чат
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.TripId && c.Type == "trip");

                if (chat != null)
                {
                    var participantNames = new List<string>();
                    if (request.SharedWith != null)
                    {
                        var participants = await _context.Users
                            .Where(u => request.SharedWith.Contains(u.IdUser))
                            .Select(u => $"{u.FirstName} {u.LastName}")
                            .ToListAsync();
                        participantNames = participants;
                    }

                    var shareText = participantNames.Any()
                        ? $" (разделено с: {string.Join(", ", participantNames)})"
                        : "";

                    var message = new ChatMessage
                    {
                        Message = $"💰 Новый расход: {request.Title} - {request.Amount} ₽{shareText}",
                        SentAt = DateTime.UtcNow,
                        SenderId = userId.Value, // Используем SenderId
                        ChatId = chat.IdChat, // Используем ChatId
                        AttachmentType = "expense",
                        AttachmentsJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            expenseId = expense.IdExpense,
                            amount = request.Amount,
                            title = request.Title,
                            categoryId = request.CategoryId
                        })
                    };

                    _context.ChatMessages.Add(message);
                    await _context.SaveChangesAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Расход добавлен и уведомление отправлено в чат",
                    Data = new { expenseId = expense.IdExpense }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании расхода из чата");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при создании расхода: " + ex.Message
                });
            }
        }
    }
}