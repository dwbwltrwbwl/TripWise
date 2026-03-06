using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TripWise.Controllers
{
    public class ChatsController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<ChatsController> _logger;

        public ChatsController(TripWiseContext context, ILogger<ChatsController> logger)
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

        [HttpGet]
        public async Task<IActionResult> GetUserChats()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<ChatDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Получаем ID чатов, в которых состоит пользователь
                var chatIds = await _context.ChatMembers
                    .Where(cm => cm.UserId == userId)
                    .Select(cm => cm.ChatId)
                    .ToListAsync();

                // Получаем чаты без лишних Include
                var chats = await _context.Chats
                    .Where(c => chatIds.Contains(c.Id))
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Description,
                        c.Type,
                        c.TripId,
                        c.CreatedAt,
                        c.CreatedById,
                        c.LastMessageAt,
                        CreatorName = c.Creator != null ? c.Creator.LastName + " " + c.Creator.FirstName : "",
                        MemberCount = c.Members.Count,
                        LastMessage = c.Messages
                            .OrderByDescending(m => m.SentAt)
                            .Select(m => new
                            {
                                m.IdMessage,
                                m.Message,
                                m.SentAt,
                                m.SenderId,
                                SenderName = m.Sender != null ? m.Sender.LastName + " " + m.Sender.FirstName : ""
                            })
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // Получаем информацию о прочитанных сообщениях для каждого чата
                var unreadCounts = new Dictionary<int, int>();

                foreach (var chat in chats)
                {
                    var lastRead = await _context.ChatMembers
                        .Where(cm => cm.ChatId == chat.Id && cm.UserId == userId)
                        .Select(cm => cm.LastReadAt)
                        .FirstOrDefaultAsync();

                    var unreadCount = await _context.ChatMessages
                        .CountAsync(m => m.ChatId == chat.Id &&
                                         m.SentAt > (lastRead ?? DateTime.MinValue) &&
                                         m.SenderId != userId);

                    unreadCounts[chat.Id] = unreadCount;
                }

                var chatDtos = chats.Select(c => new ChatDto
                {
                    Id = c.Id,
                    Name = c.Name ?? "Без названия",
                    Description = c.Description,
                    Type = c.Type ?? "private",
                    TripId = c.TripId,
                    CreatedAt = c.CreatedAt,
                    CreatedById = c.CreatedById,
                    CreatedByName = c.CreatorName,
                    LastMessageAt = c.LastMessageAt,
                    MemberCount = c.MemberCount,
                    UnreadCount = unreadCounts.ContainsKey(c.Id) ? unreadCounts[c.Id] : 0,
                    LastMessage = c.LastMessage != null ? new LastMessageDto
                    {
                        Id = c.LastMessage.IdMessage,
                        Text = c.LastMessage.Message ?? "",
                        SenderId = c.LastMessage.SenderId,
                        SenderName = c.LastMessage.SenderName,
                        SentAt = c.LastMessage.SentAt
                    } : null
                }).ToList();

                return Json(new ApiResponse<List<ChatDto>>
                {
                    Success = true,
                    Data = chatDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка чатов");
                return Json(new ApiResponse<List<ChatDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке чатов: " + ex.Message
                });
            }
        }

        // GET: /Chats/SearchUsers
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string term)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<SearchUsersResponse>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                {
                    return Json(new ApiResponse<List<SearchUsersResponse>>
                    {
                        Success = true,
                        Data = new List<SearchUsersResponse>()
                    });
                }

                var users = await _context.Users
                    .Where(u => u.IdUser != userId &&
                        (u.Email.Contains(term) ||
                         u.FirstName.Contains(term) ||
                         u.LastName.Contains(term) ||
                         (u.FirstName + " " + u.LastName).Contains(term) ||
                         (u.LastName + " " + u.FirstName).Contains(term)))
                    .Select(u => new SearchUsersResponse
                    {
                        Id = u.IdUser,
                        FullName = u.LastName + " " + u.FirstName +
                            (string.IsNullOrEmpty(u.MiddleName) ? "" : " " + u.MiddleName),
                        FirstName = u.FirstName ?? "",
                        LastName = u.LastName ?? "",
                        Email = u.Email ?? ""
                    })
                    .Take(20)
                    .ToListAsync();

                return Json(new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = true,
                    Data = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске пользователей");
                return Json(new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = false,
                    Message = "Ошибка при поиске пользователей"
                });
            }
        }

        // POST: /Chats/CreatePrivateChat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePrivateChat([FromBody] int otherUserId)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (currentUserId == null)
                {
                    return Json(new ApiResponse<int>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                if (otherUserId == currentUserId)
                {
                    return Json(new ApiResponse<int>
                    {
                        Success = false,
                        Message = "Нельзя создать чат с самим собой"
                    });
                }

                // Проверяем существующий чат
                var existingChat = await _context.Chats
                    .Where(c => c.Type == "private")
                    .Where(c => c.Members.Count == 2)
                    .Where(c => c.Members.Any(m => m.UserId == currentUserId))
                    .Where(c => c.Members.Any(m => m.UserId == otherUserId))
                    .FirstOrDefaultAsync();

                if (existingChat != null)
                {
                    return Json(new ApiResponse<int>
                    {
                        Success = true,
                        Data = existingChat.Id,
                        Message = "Чат уже существует"
                    });
                }

                // Получаем информацию о пользователях
                var currentUser = await _context.Users.FindAsync(currentUserId);
                var otherUser = await _context.Users.FindAsync(otherUserId);

                // Создаем новый чат
                var chat = new Chat
                {
                    Name = $"{currentUser?.FirstName} {currentUser?.LastName} & {otherUser?.FirstName} {otherUser?.LastName}",
                    Type = "private",
                    CreatedById = currentUserId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                // Добавляем участников
                _context.ChatMembers.Add(new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = currentUserId.Value,
                    Role = "admin",
                    JoinedAt = DateTime.UtcNow
                });

                _context.ChatMembers.Add(new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = otherUserId,
                    Role = "member",
                    JoinedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<int>
                {
                    Success = true,
                    Data = chat.Id,
                    Message = "Чат успешно создан"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании личного чата");
                return Json(new ApiResponse<int>
                {
                    Success = false,
                    Message = "Ошибка при создании чата"
                });
            }
        }
        // GET: /Chats/GetChatInfo/{chatId}
        [HttpGet("GetChatInfo/{chatId}")]
        public async Task<IActionResult> GetChatInfo(int chatId)
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

                var chat = await _context.Chats
                    .Include(c => c.Creator)
                    .Include(c => c.Members)
                        .ThenInclude(m => m.User)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Чат не найден"
                    });
                }

                // Проверяем, является ли пользователь участником чата
                var isMember = chat.Members.Any(m => m.UserId == userId);
                if (!isMember)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этому чату"
                    });
                }

                var chatInfo = new
                {
                    chat.Id,
                    chat.Name,
                    chat.Type,
                    chat.Description,
                    Members = chat.Members.Select(m => new
                    {
                        m.UserId,
                        FullName = $"{m.User.LastName} {m.User.FirstName}",
                        m.Role,
                        m.JoinedAt
                    })
                };

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { chat = chatInfo }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о чате {ChatId}", chatId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке информации о чате"
                });
            }
        }

        // GET: /Chats/GetChatMessages/{chatId}
        [HttpGet("GetChatMessages/{chatId}")]
        public async Task<IActionResult> GetChatMessages(int chatId)
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

                // Проверяем, является ли пользователь участником чата
                var isMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (!isMember)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этому чату"
                    });
                }

                var messages = await _context.ChatMessages
                    .Where(m => m.ChatId == chatId)
                    .Select(m => new
                    {
                        m.IdMessage,
                        m.Message,
                        m.SentAt,
                        SenderId = m.SenderId,
                        SenderName = m.Sender != null ? m.Sender.LastName + " " + m.Sender.FirstName : "Пользователь",
                        m.AttachmentType,
                        m.AttachmentUrl,
                        m.AttachmentName
                    })
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { messages }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сообщений чата {ChatId}", chatId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке сообщений"
                });
            }
        }

        // POST: /Chats/SendMessage
        [HttpPost("SendMessage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
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

                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Сообщение не может быть пустым"
                    });
                }

                // Проверяем, является ли пользователь участником чата
                var isMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == request.ChatId && cm.UserId == userId);

                if (!isMember)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этому чату"
                    });
                }

                var message = new ChatMessage
                {
                    ChatId = request.ChatId,
                    SenderId = userId.Value,
                    Message = request.Text,
                    SentAt = DateTime.UtcNow,
                    ReplyToId = request.ReplyToId
                };

                _context.ChatMessages.Add(message);

                // Обновляем время последнего сообщения в чате
                var chat = await _context.Chats.FindAsync(request.ChatId);
                if (chat != null)
                {
                    chat.LastMessageAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { messageId = message.IdMessage },
                    Message = "Сообщение отправлено"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке сообщения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отправке сообщения"
                });
            }
        }

        // POST: /Chats/MarkAsRead/{chatId}
        [HttpPost("MarkAsRead/{chatId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int chatId)
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

                var member = await _context.ChatMembers
                    .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (member != null)
                {
                    member.LastReadAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отметке сообщений как прочитанных");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отметке сообщений"
                });
            }
        }

        // GET: /Chats/GetNewMessages/{chatId}
        [HttpGet("GetNewMessages/{chatId}")]
        public async Task<IActionResult> GetNewMessages(int chatId, [FromQuery] int lastMessageId = 0)
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

                // Проверяем, является ли пользователь участником чата
                var isMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (!isMember)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этому чату"
                    });
                }

                var messages = await _context.ChatMessages
                    .Where(m => m.ChatId == chatId && m.IdMessage > lastMessageId)
                    .Include(m => m.Sender)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        m.IdMessage,
                        m.Message,
                        m.SentAt,
                        SenderId = m.SenderId,
                        SenderName = m.Sender != null ? $"{m.Sender.LastName} {m.Sender.FirstName}" : "Пользователь",
                        m.AttachmentType,
                        m.AttachmentUrl,
                        m.AttachmentName
                    })
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { messages }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении новых сообщений");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке новых сообщений"
                });
            }
        }

        // POST: /Chats/CreateGroupChat
        [HttpPost("CreateGroupChat")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroupChat([FromBody] CreateChatRequest request)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (currentUserId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                if (request.UserIds == null || request.UserIds.Count == 0)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Выберите хотя бы одного участника"
                    });
                }

                // Добавляем создателя в список участников
                var allUserIds = request.UserIds.Distinct().ToList();
                if (!allUserIds.Contains(currentUserId.Value))
                {
                    allUserIds.Add(currentUserId.Value);
                }

                // Создаем название чата, если не указано
                string chatName = request.Name;
                if (string.IsNullOrWhiteSpace(chatName))
                {
                    var users = await _context.Users
                        .Where(u => allUserIds.Contains(u.IdUser))
                        .Take(3)
                        .ToListAsync();

                    chatName = string.Join(", ", users.Select(u => $"{u.FirstName} {u.LastName}"));
                    if (allUserIds.Count > 3)
                    {
                        chatName += $" и еще {allUserIds.Count - 3}";
                    }
                }

                // Создаем чат
                var chat = new Chat
                {
                    Name = chatName,
                    Description = request.Description,
                    Type = "group",
                    TripId = request.TripId,
                    CreatedById = currentUserId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                // Добавляем участников
                var chatMembers = allUserIds.Select(userId => new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = userId,
                    Role = userId == currentUserId ? "admin" : "member",
                    JoinedAt = DateTime.UtcNow
                });

                _context.ChatMembers.AddRange(chatMembers);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { chatId = chat.Id },
                    Message = "Чат успешно создан"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании группового чата");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при создании чата"
                });
            }
        }

        // GET: /Chats/SearchUsersToAdd
        [HttpGet("SearchUsersToAdd")]
        public async Task<IActionResult> SearchUsersToAdd(int chatId, string term)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<SearchUsersResponse>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                {
                    return Json(new ApiResponse<List<SearchUsersResponse>>
                    {
                        Success = true,
                        Data = new List<SearchUsersResponse>()
                    });
                }

                // Получаем ID текущих участников чата
                var currentMemberIds = await _context.ChatMembers
                    .Where(cm => cm.ChatId == chatId)
                    .Select(cm => cm.UserId)
                    .ToListAsync();

                // Ищем пользователей, которые не являются участниками чата
                var users = await _context.Users
                    .Where(u => !currentMemberIds.Contains(u.IdUser) &&
                        (u.Email.Contains(term) ||
                         u.FirstName.Contains(term) ||
                         u.LastName.Contains(term) ||
                         (u.FirstName + " " + u.LastName).Contains(term)))
                    .Select(u => new SearchUsersResponse
                    {
                        Id = u.IdUser,
                        FullName = u.LastName + " " + u.FirstName +
                            (string.IsNullOrEmpty(u.MiddleName) ? "" : " " + u.MiddleName),
                        FirstName = u.FirstName ?? "",
                        LastName = u.LastName ?? "",
                        Email = u.Email ?? ""
                    })
                    .Take(20)
                    .ToListAsync();

                return Json(new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = true,
                    Data = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске пользователей для добавления");
                return Json(new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = false,
                    Message = "Ошибка при поиске пользователей"
                });
            }
        }

        // POST: /Chats/AddMember
        [HttpPost("AddMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember([FromBody] AddMemberRequest request)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (currentUserId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Проверяем, является ли текущий пользователь администратором чата
                var isAdmin = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == request.ChatId &&
                                   cm.UserId == currentUserId &&
                                   cm.Role == "admin");

                if (!isAdmin)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только администратор может добавлять участников"
                    });
                }

                // Проверяем, не является ли пользователь уже участником
                var isAlreadyMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == request.ChatId && cm.UserId == request.UserId);

                if (isAlreadyMember)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь уже в чате"
                    });
                }

                // Добавляем участника
                var member = new ChatMember
                {
                    ChatId = request.ChatId,
                    UserId = request.UserId,
                    Role = "member",
                    JoinedAt = DateTime.UtcNow
                };

                _context.ChatMembers.Add(member);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Участник добавлен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении участника");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при добавлении участника"
                });
            }
        }

        // POST: /Chats/LeaveChat/{chatId}
        [HttpPost("LeaveChat/{chatId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveChat(int chatId)
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

                var member = await _context.ChatMembers
                    .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (member == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Вы не являетесь участником этого чата"
                    });
                }

                _context.ChatMembers.Remove(member);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Вы покинули чат"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выходе из чата");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при выходе из чата"
                });
            }
        }
        // GET: /Chats/GetFriendsList
        [HttpGet("GetFriendsList")]
        public async Task<IActionResult> GetFriendsList()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<object>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var friends = await _context.Friends
                    .Include(f => f.FriendUser)
                    .Where(f => f.UserId == userId && f.Status == "accepted")
                    .Select(f => new
                    {
                        f.FriendId,
                        FullName = f.FriendUser.LastName + " " + f.FriendUser.FirstName +
                                  (string.IsNullOrEmpty(f.FriendUser.MiddleName) ? "" : " " + f.FriendUser.MiddleName),
                        f.FriendUser.AvatarPath,
                        HasPrivateChat = _context.Chats
                            .Any(c => c.Type == "private" &&
                                c.Members.Count == 2 &&
                                c.Members.Any(m => m.UserId == userId) &&
                                c.Members.Any(m => m.UserId == f.FriendId))
                    })
                    .OrderBy(f => f.FullName)
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { friends }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка друзей");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке друзей"
                });
            }
        }

        // GET: /Chats/GetFriendsForGroup
        [HttpGet("GetFriendsForGroup")]
        public async Task<IActionResult> GetFriendsForGroup()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<object>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var friends = await _context.Friends
                    .Include(f => f.FriendUser)
                    .Where(f => f.UserId == userId && f.Status == "accepted")
                    .Select(f => new
                    {
                        f.FriendId,
                        FullName = f.FriendUser.LastName + " " + f.FriendUser.FirstName +
                                  (string.IsNullOrEmpty(f.FriendUser.MiddleName) ? "" : " " + f.FriendUser.MiddleName),
                        f.FriendUser.AvatarPath
                    })
                    .OrderBy(f => f.FullName)
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { friends }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка друзей");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке друзей"
                });
            }
        }

        // GET: /Chats/GetTripParticipants/{tripId}
        [HttpGet("GetTripParticipants/{tripId}")]
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
    .Include(tp => tp.IdParticipantRoleNavigation)  
    .Where(tp => tp.IdTrip == tripId)
    .Select(tp => new
    {
        tp.IdUser,
        FullName = tp.IdUserNavigation.LastName + " " + tp.IdUserNavigation.FirstName +
                  (string.IsNullOrEmpty(tp.IdUserNavigation.MiddleName) ? "" : " " + tp.IdUserNavigation.MiddleName),
        tp.IdUserNavigation.AvatarPath,
        Role = tp.IdParticipantRoleNavigation != null ? tp.IdParticipantRoleNavigation.ParticipantRole1 : "Участник",
        IsFriend = _context.Friends
            .Any(f => f.UserId == userId && f.FriendId == tp.IdUser && f.Status == "accepted")
    })
    .OrderBy(p => p.FullName)
    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { participants }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении участников поездки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке участников"
                });
            }
        }
    }
}