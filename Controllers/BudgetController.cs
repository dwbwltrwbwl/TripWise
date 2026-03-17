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
                var userTrips = await _context.TripParticipants
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
                    .Where(e => userTrips.Contains(e.IdTrip))
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

                // Формируем DTO для категорий
                var categoryDtos = categories.Select(c =>
                {
                    // Вычисляем сумму расходов по этой категории
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

                // Получаем информацию о поездках
                var trips = await _context.Trips
                    .Where(t => userTrips.Contains(t.IdTrip))
                    .Select(t => new TripBudgetDto
                    {
                        Id = t.IdTrip,
                        Title = t.Title ?? "Без названия",
                        StartDate = t.StartDate,
                        EndDate = t.EndDate,
                        TotalBudget = t.TotalBudget,
                        TotalSpent = expenses.Where(e => e.IdTrip == t.IdTrip).Sum(e => e.Amount),
                        ParticipantCount = t.TripParticipants.Count,
                        Participants = t.TripParticipants
                            .Select(tp => tp.IdUserNavigation.LastName + " " + tp.IdUserNavigation.FirstName)
                            .ToList()
                    })
                    .ToListAsync();

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
                        ? $"{e.PaidBy.LastName} {e.PaidBy.FirstName}"
                        : "Неизвестно",
                    PaidById = e.PaidById ?? 0,
                    Shares = e.ExpenseShares.Select(es => new ExpenseShareDto
                    {
                        UserId = es.IdUser,
                        UserName = es.IdUserNavigation != null
                            ? $"{es.IdUserNavigation.LastName} {es.IdUserNavigation.FirstName}"
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
    }
}