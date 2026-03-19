using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System.Security.Claims;
using Microsoft.Data.SqlClient;

namespace TripWise.Controllers
{
    public class TripsController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<TripsController> _logger;

        public TripsController(TripWiseContext context, ILogger<TripsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Trips
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        // GET: /Trips/GetUserTrips
        [HttpGet]
        public async Task<IActionResult> GetUserTrips()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<TripListDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("GetUserTrips для пользователя {UserId}", userId);

                // Получаем все поездки, где пользователь является участником
                var userTripIds = await _context.TripParticipants
                    .Where(tp => tp.IdUser == userId)
                    .Select(tp => tp.IdTrip)
                    .ToListAsync();

                var now = DateTime.UtcNow;

                // Загружаем поездки с полной информацией
                var trips = await _context.Trips
                    .Where(t => userTripIds.Contains(t.IdTrip))
                    .Include(t => t.CreatedBy)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdUserNavigation)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdParticipantRoleNavigation)
                    .Include(t => t.PointsOfInterests)
                        .ThenInclude(p => p.IdInterestCategoryNavigation)
                    .Include(t => t.Expenses)
                    .ToListAsync();

                // Получаем чаты для поездок отдельным запросом
                var tripChats = await _context.Chats
                    .Where(c => c.IdTrip.HasValue && userTripIds.Contains(c.IdTrip.Value))
                    .Select(c => new { c.IdTrip, c.IdChat })
                    .ToDictionaryAsync(c => c.IdTrip.Value, c => c.IdChat);

                // Формируем DTO
                var tripDtos = trips.Select(t =>
                {
                    // Определяем статус поездки
                    string status;
                    if (t.EndDate < now)
                        status = "completed";
                    else if (t.StartDate <= now && t.EndDate >= now)
                        status = "active";
                    else
                        status = "upcoming";

                    // Получаем список участников с информацией о друзьях
                    var participants = t.TripParticipants.Select(tp => new TripParticipantDto
                    {
                        UserId = tp.IdUser,
                        FullName = $"{tp.IdUserNavigation?.LastName ?? ""} {tp.IdUserNavigation?.FirstName ?? ""}".Trim(),
                        AvatarPath = tp.IdUserNavigation?.AvatarPath,
                        Role = tp.IdParticipantRoleNavigation?.ParticipantRole1 ?? "Участник", // ИСПРАВЛЕНО: ParticipantRole1
                        IsFriend = _context.Friends.Any(f =>
                            (f.UserId == userId && f.FriendId == tp.IdUser && f.Status == "accepted") ||
                            (f.UserId == tp.IdUser && f.FriendId == userId && f.Status == "accepted"))
                    }).ToList();

                    return new TripListDto
                    {
                        Id = t.IdTrip,
                        Title = t.Title ?? "Без названия",
                        Description = t.Description,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate,
                        TotalBudget = t.TotalBudget,
                        Status = status,
                        ParticipantCount = participants.Count(), // ИСПРАВЛЕНО: добавили ()
                        Participants = participants,
                        ChatId = tripChats.ContainsKey(t.IdTrip) ? tripChats[t.IdTrip] : (int?)null,
                        CoverImage = GetTripCoverImage(t),
                        CreatedAt = t.CreatedAt,
                        CreatedBy = new TripCreatorDto
                        {
                            Id = t.CreatedBy?.IdUser ?? 0,
                            FullName = t.CreatedBy != null
                                ? $"{t.CreatedBy.LastName} {t.CreatedBy.FirstName}".Trim()
                                : "Система",
                            AvatarPath = t.CreatedBy?.AvatarPath
                        },
                        PointsCount = t.PointsOfInterests?.Count() ?? 0, // ИСПРАВЛЕНО: добавили ()
                        SpentBudget = t.Expenses?.Sum(e => e.Amount) ?? 0
                    };
                }).ToList();

                // Разделяем на предстоящие и завершенные
                var upcomingTrips = tripDtos.Where(t => t.Status != "completed").OrderBy(t => t.StartDate).ToList();
                var completedTrips = tripDtos.Where(t => t.Status == "completed").OrderByDescending(t => t.EndDate).ToList();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        upcoming = upcomingTrips,
                        completed = completedTrips,
                        all = tripDtos
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении поездок пользователя");
                return Json(new ApiResponse<List<TripListDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке поездок: " + ex.Message
                });
            }
        }

        // POST: /Trips/CreateTrip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrip([FromBody] CreateTripRequest request)
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

                _logger.LogInformation("CreateTrip: userId={UserId}, title={Title}", userId, request.Title);

                // Проверяем даты
                if (request.EndDate <= request.StartDate)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Дата окончания должна быть позже даты начала"
                    });
                }

                // Создаем поездку
                var trip = new Trip
                {
                    Title = request.Title,
                    Description = request.Description,
                    StartDate = request.StartDate.ToUniversalTime(),
                    EndDate = request.EndDate.ToUniversalTime(),
                    TotalBudget = request.TotalBudget,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId.Value
                };

                _context.Trips.Add(trip);
                await _context.SaveChangesAsync();

                // Добавляем создателя как участника
                var participant = new TripParticipant
                {
                    IdTrip = trip.IdTrip,
                    IdUser = userId.Value,
                    IdParticipantRole = 1, // Организатор
                    JoinedAt = DateTime.UtcNow
                };
                _context.TripParticipants.Add(participant);

                // Если поездка публичная, создаем чат для нее
                Chat? chat = null;
                if (request.IsPublic)
                {
                    chat = new Chat
                    {
                        Name = $"Чат: {trip.Title}",
                        Type = "trip",
                        IdTrip = trip.IdTrip,
                        CreatedById = userId.Value,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Chats.Add(chat);
                    await _context.SaveChangesAsync();

                    // Добавляем создателя в чат
                    _context.ChatMembers.Add(new ChatMember
                    {
                        ChatId = chat.IdChat,
                        UserId = userId.Value,
                        Role = "admin",
                        JoinedAt = DateTime.UtcNow
                    });
                }

                // Приглашаем друзей, если указаны
                if (request.InvitedFriends != null && request.InvitedFriends.Any())
                {
                    foreach (var friendId in request.InvitedFriends.Distinct())
                    {
                        if (friendId != userId.Value)
                        {
                            // Добавляем как участника поездки
                            _context.TripParticipants.Add(new TripParticipant
                            {
                                IdTrip = trip.IdTrip,
                                IdUser = friendId,
                                IdParticipantRole = 2, // Участник
                                JoinedAt = DateTime.UtcNow
                            });

                            // Если есть чат, добавляем и туда
                            if (chat != null)
                            {
                                _context.ChatMembers.Add(new ChatMember
                                {
                                    ChatId = chat.IdChat,
                                    UserId = friendId,
                                    Role = "member",
                                    JoinedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Поездка успешно создана",
                    Data = new { tripId = trip.IdTrip }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании поездки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при создании поездки: " + ex.Message
                });
            }
        }

        // POST: /Trips/InviteFriends
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteFriends([FromBody] InviteFriendsRequest request)
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

                _logger.LogInformation("InviteFriends: tripId={TripId}, userId={UserId}", request.TripId, userId);

                // Проверяем, является ли пользователь организатором поездки
                var isOrganizer = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId &&
                                   tp.IdUser == userId &&
                                   tp.IdParticipantRole == 1);

                if (!isOrganizer)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только организатор может приглашать друзей"
                    });
                }

                // Получаем текущих участников
                var currentParticipants = await _context.TripParticipants
                    .Where(tp => tp.IdTrip == request.TripId)
                    .Select(tp => tp.IdUser)
                    .ToListAsync();

                // Получаем чат поездки
                var tripChat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.TripId && c.Type == "trip");

                // Добавляем новых участников
                foreach (var friendId in request.FriendIds.Distinct())
                {
                    if (!currentParticipants.Contains(friendId) && friendId != userId)
                    {
                        // Добавляем в поездку
                        _context.TripParticipants.Add(new TripParticipant
                        {
                            IdTrip = request.TripId,
                            IdUser = friendId,
                            IdParticipantRole = 2, // Участник
                            JoinedAt = DateTime.UtcNow
                        });

                        // Добавляем в чат, если он есть
                        if (tripChat != null)
                        {
                            var isInChat = await _context.ChatMembers
                                .AnyAsync(cm => cm.ChatId == tripChat.IdChat && cm.UserId == friendId);

                            if (!isInChat)
                            {
                                _context.ChatMembers.Add(new ChatMember
                                {
                                    ChatId = tripChat.IdChat,
                                    UserId = friendId,
                                    Role = "member",
                                    JoinedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Друзья приглашены в поездку"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приглашении друзей");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при приглашении друзей: " + ex.Message
                });
            }
        }

        // GET: /Trips/GetTripDetails/5
        [HttpGet]
        public async Task<IActionResult> GetTripDetails(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<TripDetailDto>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Проверяем, является ли пользователь участником
                var isParticipant = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == id && tp.IdUser == userId);

                if (!isParticipant)
                {
                    return Json(new ApiResponse<TripDetailDto>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этой поездке"
                    });
                }

                var trip = await _context.Trips
                    .Include(t => t.CreatedBy)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdUserNavigation)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdParticipantRoleNavigation)
                    .Include(t => t.PointsOfInterests)
                        .ThenInclude(p => p.IdInterestCategoryNavigation)
                    .Include(t => t.Expenses)
                        .ThenInclude(e => e.IdExpenseCategoryNavigation)
                    .FirstOrDefaultAsync(t => t.IdTrip == id);

                if (trip == null)
                {
                    return Json(new ApiResponse<TripDetailDto>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Получаем чат поездки
                var tripChat = await _context.Chats
                    .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(5))
                    .FirstOrDefaultAsync(c => c.IdTrip == id && c.Type == "trip");

                var now = DateTime.UtcNow;
                string status;
                if (trip.EndDate < now)
                    status = "completed";
                else if (trip.StartDate <= now && trip.EndDate >= now)
                    status = "active";
                else
                    status = "upcoming";

                var participants = trip.TripParticipants.Select(tp => new TripParticipantDto
                {
                    UserId = tp.IdUser,
                    FullName = $"{tp.IdUserNavigation?.LastName ?? ""} {tp.IdUserNavigation?.FirstName ?? ""}".Trim(),
                    AvatarPath = tp.IdUserNavigation?.AvatarPath,
                    Role = tp.IdParticipantRoleNavigation?.ParticipantRole1 ?? "Участник", // ИСПРАВЛЕНО: ParticipantRole1
                    IsFriend = _context.Friends.Any(f =>
                        (f.UserId == userId && f.FriendId == tp.IdUser && f.Status == "accepted") ||
                        (f.UserId == tp.IdUser && f.FriendId == userId && f.Status == "accepted"))
                }).ToList();

                var points = trip.PointsOfInterests?.Select(p => new PointOfInterestDto
                {
                    Id = p.IdPoint,
                    Name = p.Name ?? "Без названия",
                    Description = p.Description,
                    Cost = p.Cost,
                    PlannedDate = p.PlannedDate,
                    Category = p.IdInterestCategoryNavigation?.InterestCategory1 ?? "Другое" // ИСПРАВЛЕНО: InterestCategory1
                }).ToList() ?? new List<PointOfInterestDto>();

                var expenses = trip.Expenses?.Select(e => new ExpenseDto
                {
                    Id = e.IdExpense,
                    Title = e.Title ?? "Без названия",
                    Amount = e.Amount,
                    Category = e.IdExpenseCategoryNavigation?.ExpenseCategoryName ?? "Другое",
                    Date = e.ExpenseDate,
                    PaidBy = _context.Users
                        .Where(u => u.IdUser == e.PaidById)
                        .Select(u => $"{u.LastName} {u.FirstName}".Trim())
                        .FirstOrDefault() ?? "Неизвестно"
                }).ToList() ?? new List<ExpenseDto>();

                var recentMessages = tripChat?.Messages?.Select(m => new TripMessageDto
                {
                    Id = m.IdMessage,
                    Text = m.Message ?? "",
                    SenderName = _context.Users
                        .Where(u => u.IdUser == m.SenderId)
                        .Select(u => $"{u.LastName} {u.FirstName}".Trim())
                        .FirstOrDefault() ?? "Пользователь",
                    SentAt = m.SentAt
                }).ToList() ?? new List<TripMessageDto>();

                var dto = new TripDetailDto
                {
                    Id = trip.IdTrip,
                    Title = trip.Title ?? "Без названия",
                    Description = trip.Description,
                    StartDate = trip.StartDate,
                    EndDate = trip.EndDate,
                    TotalBudget = trip.TotalBudget,
                    Status = status,
                    ParticipantCount = participants.Count(), // ИСПРАВЛЕНО: добавили ()
                    Participants = participants,
                    ChatId = tripChat?.IdChat,
                    CoverImage = GetTripCoverImage(trip),
                    CreatedAt = trip.CreatedAt,
                    CreatedBy = new TripCreatorDto
                    {
                        Id = trip.CreatedBy?.IdUser ?? 0,
                        FullName = trip.CreatedBy != null
                            ? $"{trip.CreatedBy.LastName} {trip.CreatedBy.FirstName}".Trim()
                            : "Система",
                        AvatarPath = trip.CreatedBy?.AvatarPath
                    },
                    PointsCount = points.Count(), // ИСПРАВЛЕНО: добавили ()
                    SpentBudget = expenses.Sum(e => e.Amount),
                    Points = points,
                    Expenses = expenses.OrderByDescending(e => e.Date).ToList(),
                    RecentMessages = recentMessages
                };

                return Json(new ApiResponse<TripDetailDto>
                {
                    Success = true,
                    Data = dto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении деталей поездки {TripId}", id);
                return Json(new ApiResponse<TripDetailDto>
                {
                    Success = false,
                    Message = "Ошибка при загрузке деталей поездки: " + ex.Message
                });
            }
        }

        // GET: /Trips/GetFriendsForInvite
        [HttpGet]
        public async Task<IActionResult> GetFriendsForInvite(int tripId)
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

                // Получаем текущих участников поездки
                var currentParticipants = await _context.TripParticipants
                    .Where(tp => tp.IdTrip == tripId)
                    .Select(tp => tp.IdUser)
                    .ToListAsync();

                // Получаем друзей, которые еще не в поездке
                var friends = await _context.Friends
                    .Include(f => f.FriendUser)
                    .Where(f => f.UserId == userId && f.Status == "accepted")
                    .Select(f => new
                    {
                        f.FriendId,
                        FullName = f.FriendUser.LastName + " " + f.FriendUser.FirstName,
                        f.FriendUser.AvatarPath,
                        IsInTrip = currentParticipants.Contains(f.FriendId)
                    })
                    .Where(f => !f.IsInTrip)
                    .OrderBy(f => f.FullName)
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = friends
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении друзей для приглашения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке друзей"
                });
            }
        }

        private string GetTripCoverImage(Trip trip)
        {
            // Здесь можно добавить логику для получения обложки поездки
            // Например, из первой точки интереса или загруженного изображения
            return null;
        }
        // POST: /Trips/DeleteTrip/5?deleteChat=true
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrip(int id, [FromQuery] bool deleteChat = true)
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

                _logger.LogInformation("DeleteTrip: tripId={TripId}, userId={UserId}, deleteChat={DeleteChat}",
                    id, userId, deleteChat);

                // Находим поездку
                var trip = await _context.Trips
                    .Include(t => t.TripParticipants)
                    .FirstOrDefaultAsync(t => t.IdTrip == id);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли пользователь создателем поездки
                if (trip.CreatedById != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только создатель может удалить поездку"
                    });
                }

                // Получаем связанные чаты (типа "trip")
                var tripChats = await _context.Chats
                    .Where(c => c.IdTrip == id && c.Type == "trip")
                    .ToListAsync();

                // Удаляем чаты только если пользователь выбрал эту опцию
                if (deleteChat && tripChats != null && tripChats.Any())
                {
                    _context.Chats.RemoveRange(tripChats);
                    _logger.LogInformation("Удалено {Count} чатов для поездки {TripId}", tripChats.Count, id);
                }
                else if (tripChats != null && tripChats.Any())
                {
                    // Если чаты не удаляем, отвязываем их от поездки
                    foreach (var chat in tripChats)
                    {
                        chat.IdTrip = null;
                    }
                    _logger.LogInformation("Чаты отвязаны от поездки {TripId}", id);
                }

                // Удаляем поездку
                _context.Trips.Remove(trip);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = deleteChat
                        ? "Поездка и связанный чат успешно удалены"
                        : "Поездка успешно удалена, чат сохранен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении поездки {TripId}", id);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении поездки: " + ex.Message
                });
            }
        }
        // POST: /Trips/UpdateTrip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTrip([FromBody] UpdateTripRequest request)
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

                _logger.LogInformation("UpdateTrip: tripId={TripId}, userId={UserId}", request.Id, userId);

                // Находим поездку
                var trip = await _context.Trips
                    .FirstOrDefaultAsync(t => t.IdTrip == request.Id);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли пользователь создателем
                if (trip.CreatedById != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только создатель может редактировать поездку"
                    });
                }

                // Проверяем, что поездка не завершена
                var now = DateTime.UtcNow;
                if (trip.EndDate < now)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нельзя редактировать завершенные поездки"
                    });
                }

                // Проверяем даты
                if (request.EndDate <= request.StartDate)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Дата окончания должна быть позже даты начала"
                    });
                }

                // Обновляем данные поездки
                trip.Title = request.Title;
                trip.Description = request.Description;
                trip.StartDate = request.StartDate.ToUniversalTime();
                trip.EndDate = request.EndDate.ToUniversalTime();
                trip.TotalBudget = request.TotalBudget;

                await _context.SaveChangesAsync();

                // Проверяем, есть ли уже чат у поездки
                var existingChat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.Id && c.Type == "trip");

                // Если пользователь хочет публичный чат
                if (request.IsPublic)
                {
                    if (existingChat == null)
                    {
                        // Создаем новый чат
                        var newChat = new Chat
                        {
                            Name = $"Чат: {trip.Title}",
                            Type = "trip",
                            IdTrip = trip.IdTrip,
                            CreatedById = userId.Value,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Chats.Add(newChat);
                        await _context.SaveChangesAsync();

                        // Добавляем создателя в чат
                        _context.ChatMembers.Add(new ChatMember
                        {
                            ChatId = newChat.IdChat,
                            UserId = userId.Value,
                            Role = "admin",
                            JoinedAt = DateTime.UtcNow
                        });

                        // Добавляем всех участников поездки в чат
                        var participants = await _context.TripParticipants
                            .Where(tp => tp.IdTrip == trip.IdTrip && tp.IdUser != userId)
                            .Select(tp => tp.IdUser)
                            .ToListAsync();

                        foreach (var participantId in participants)
                        {
                            _context.ChatMembers.Add(new ChatMember
                            {
                                ChatId = newChat.IdChat,
                                UserId = participantId,
                                Role = "member",
                                JoinedAt = DateTime.UtcNow
                            });
                        }

                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Создан новый чат для поездки {TripId}", trip.IdTrip);
                    }
                    else
                    {
                        // Обновляем название существующего чата
                        existingChat.Name = $"Чат: {trip.Title}";
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // Если пользователь не хочет публичный чат, но чат существует - ничего не делаем
                    // Чат остается, но его можно будет удалить отдельно
                    if (existingChat != null)
                    {
                        _logger.LogInformation("Чат для поездки {TripId} существует, но оставлен по желанию пользователя", trip.IdTrip);
                    }
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = existingChat == null && request.IsPublic
                        ? "Поездка обновлена и создан новый чат"
                        : "Поездка успешно обновлена"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении поездки {TripId}", request?.Id);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении поездки: " + ex.Message
                });
            }
        }

        // GET: /Trips/GetTripForEdit/5
        [HttpGet]
        public async Task<IActionResult> GetTripForEdit(int id)
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

                var trip = await _context.Trips
                    .FirstOrDefaultAsync(t => t.IdTrip == id);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли пользователь создателем
                if (trip.CreatedById != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только создатель может редактировать поездку"
                    });
                }

                // Проверяем, что поездка не завершена
                var now = DateTime.UtcNow;
                if (trip.EndDate < now)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нельзя редактировать завершенные поездки"
                    });
                }

                // Проверяем, есть ли у поездки чат
                var hasChat = await _context.Chats
                    .AnyAsync(c => c.IdTrip == id && c.Type == "trip");

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        trip.IdTrip,
                        trip.Title,
                        trip.Description,
                        StartDate = trip.StartDate.ToString("yyyy-MM-dd"),
                        EndDate = trip.EndDate.ToString("yyyy-MM-dd"),
                        trip.TotalBudget,
                        HasChat = hasChat,
                        // Если чат уже есть, чекбокс будет включен, иначе выключен
                        IsPublic = hasChat
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных для редактирования поездки {TripId}", id);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке данных: " + ex.Message
                });
            }
        }
    }
}