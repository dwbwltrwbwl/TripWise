using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

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

        // GET: /Chats/GetUserChats
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

                _logger.LogInformation("GetUserChats: Загрузка чатов для пользователя {UserId}", userId);

                // Получаем все чаты, где пользователь является участником
                var chatIds = await _context.ChatMembers
                    .Where(cm => cm.UserId == userId)
                    .Select(cm => cm.ChatId)
                    .ToListAsync();

                _logger.LogInformation("Найдено {Count} ID чатов для пользователя {UserId}", chatIds.Count, userId);

                // Загружаем чаты с полной информацией
                var chats = await _context.Chats
                    .Where(c => chatIds.Contains(c.IdChat))
                    .Include(c => c.Creator)
                    .Include(c => c.Trip)
                    .Include(c => c.Members)
                        .ThenInclude(m => m.User)
                    .Include(c => c.Messages)
                        .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                    .Select(c => new
                    {
                        Id = c.IdChat,
                        c.Name,
                        c.Description,
                        c.Type,
                        c.IdTrip,
                        TripName = c.Trip != null ? c.Trip.Title : null,
                        c.CreatedAt,
                        c.CreatedById,
                        c.LastMessageAt,

                        CreatorName = c.Creator != null
                            ? (c.Creator.LastName + " " + c.Creator.FirstName).Trim()
                            : "Система",

                        MemberCount = c.Members.Count,

                        // Получаем последнее сообщение
                        LastMessage = c.Messages
                            .OrderByDescending(m => m.SentAt)
                            .Select(m => new
                            {
                                m.IdMessage,
                                m.Message,
                                m.SentAt,
                                m.SenderId,
                                SenderName = m.Sender != null
                                    ? (m.Sender.LastName + " " + m.Sender.FirstName).Trim()
                                    : "Пользователь"
                            })
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                _logger.LogInformation("Загружено {Count} чатов", chats.Count);

                // Для каждого чата получаем количество непрочитанных сообщений
                var unreadCounts = new Dictionary<int, int>();

                foreach (var chat in chats)
                {
                    // Получаем время последнего прочтения для пользователя в этом чате
                    var lastRead = await _context.ChatMembers
                        .Where(cm => cm.ChatId == chat.Id && cm.UserId == userId)
                        .Select(cm => cm.LastReadAt)
                        .FirstOrDefaultAsync();

                    // Считаем непрочитанные сообщения (отправленные после lastRead и не от текущего пользователя)
                    var unreadCount = await _context.ChatMessages
                        .CountAsync(m =>
                            m.ChatId == chat.Id &&
                            m.SentAt > (lastRead ?? DateTime.MinValue) &&
                            m.SenderId != userId);

                    unreadCounts[chat.Id] = unreadCount;
                }

                // Преобразуем в DTO для отправки клиенту
                var chatDtos = chats.Select(c => new ChatDto
                {
                    Id = c.Id,
                    Name = c.Name ?? "Чат",
                    Description = c.Description,
                    Type = c.Type ?? "group",
                    TripId = c.IdTrip,
                    TripName = c.TripName,
                    CreatedAt = c.CreatedAt,
                    CreatedById = c.CreatedById,
                    CreatedByName = c.CreatorName,
                    LastMessageAt = c.LastMessageAt,
                    MemberCount = c.MemberCount,
                    UnreadCount = unreadCounts.ContainsKey(c.Id) ? unreadCounts[c.Id] : 0,

                    LastMessage = c.LastMessage != null
                        ? new LastMessageDto
                        {
                            Id = c.LastMessage.IdMessage,
                            Text = c.LastMessage.Message ?? "",
                            SenderId = c.LastMessage.SenderId,
                            SenderName = c.LastMessage.SenderName ?? "Пользователь",
                            SentAt = c.LastMessage.SentAt
                        }
                        : null
                }).OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).ToList();

                return Json(new ApiResponse<List<ChatDto>>
                {
                    Success = true,
                    Data = chatDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки чатов для пользователя");
                return Json(new ApiResponse<List<ChatDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке чатов: " + ex.Message
                });
            }
        }

        // GET: /Chats/GetChatInfo?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetChatInfo(int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");

                if (userId == null)
                    return Json(new ApiResponse<ChatDetailDto>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });

                _logger.LogInformation("GetChatInfo: chatId={ChatId}, userId={UserId}", chatId, userId);

                // Проверяем, существует ли чат
                var chatExists = await _context.Chats.AnyAsync(c => c.IdChat == chatId);
                _logger.LogInformation("Чат с ID {ChatId} существует в БД: {Exists}", chatId, chatExists);

                if (!chatExists)
                {
                    return Json(new ApiResponse<ChatDetailDto>
                    {
                        Success = false,
                        Message = $"Чат с ID {chatId} не найден в базе данных"
                    });
                }

                var chat = await _context.Chats
                    .Include(c => c.Creator)
                    .Include(c => c.Members)
                        .ThenInclude(m => m.User)
                    .Include(c => c.Trip)
                    .FirstOrDefaultAsync(c => c.IdChat == chatId);

                if (chat == null)
                {
                    return Json(new ApiResponse<ChatDetailDto>
                    {
                        Success = false,
                        Message = "Чат не найден"
                    });
                }

                // Проверяем, является ли пользователь участником чата
                var memberCheck = chat.Members?.Any(m => m.UserId == userId) ?? false;
                _logger.LogInformation("Пользователь {UserId} является участником чата {ChatId}: {IsMember}",
                    userId, chatId, memberCheck);

                if (!memberCheck)
                {
                    return Json(new ApiResponse<ChatDetailDto>
                    {
                        Success = false,
                        Message = $"У вас нет доступа к этому чату. Вы не являетесь участником чата {chatId}"
                    });
                }

                var totalMessages = await _context.ChatMessages
                    .CountAsync(m => m.ChatId == chatId);

                var dto = new ChatDetailDto
                {
                    Id = chat.IdChat,
                    Name = chat.Name,
                    Description = chat.Description,
                    Type = chat.Type,
                    TripId = chat.IdTrip,
                    TripName = chat.Trip?.Title,
                    CreatedAt = chat.CreatedAt,

                    Creator = chat.Creator != null
                        ? new UserDto
                        {
                            Id = chat.Creator.IdUser,
                            FullName = chat.Creator.LastName + " " + chat.Creator.FirstName,
                            FirstName = chat.Creator.FirstName,
                            LastName = chat.Creator.LastName,
                            Email = chat.Creator.Email
                        }
                        : null,

                    Members = chat.Members?.Select(m => new ChatMemberDto
                    {
                        UserId = m.UserId,
                        FullName = m.User != null
                            ? $"{m.User.LastName} {m.User.FirstName}"
                            : "Unknown",
                        Email = m.User?.Email,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt,
                        LastReadAt = m.LastReadAt
                    }).ToList() ?? new List<ChatMemberDto>(),

                    TotalMessages = totalMessages
                };

                return Json(new ApiResponse<ChatDetailDto>
                {
                    Success = true,
                    Data = dto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка GetChatInfo для chatId={ChatId}", chatId);
                return Json(new ApiResponse<ChatDetailDto>
                {
                    Success = false,
                    Message = "Ошибка сервера: " + ex.Message
                });
            }
        }

        // GET: /Chats/GetChatMessages?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetChatMessages(int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<ChatMessageDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("========== GetChatMessages ==========");
                _logger.LogInformation("chatId={ChatId}, userId={UserId}", chatId, userId);

                // Проверяем, является ли пользователь участником чата
                var isMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                _logger.LogInformation("Пользователь {UserId} является участником чата {ChatId}? {IsMember}",
                    userId, chatId, isMember);

                if (!isMember)
                {
                    return Json(new ApiResponse<List<ChatMessageDto>>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этому чату"
                    });
                }

                // Загружаем все сообщения - БЕЗ Include и навигационных свойств
                var messages = await _context.ChatMessages
                    .Where(m => m.ChatId == chatId)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        m.IdMessage,
                        m.Message,
                        m.SentAt,
                        m.EditedAt,
                        m.SenderId,
                        m.ReplyToId,
                        m.AttachmentType,
                        m.AttachmentUrl,
                        m.AttachmentName,
                        m.AttachmentSize
                    })
                    .ToListAsync();

                // Загружаем информацию об отправителях отдельно
                var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
                var senders = await _context.Users
                    .Where(u => senderIds.Contains(u.IdUser))
                    .Select(u => new { u.IdUser, u.LastName, u.FirstName })
                    .ToDictionaryAsync(u => u.IdUser, u => $"{u.LastName} {u.FirstName}".Trim());

                // Загружаем информацию о reply-to сообщениях
                var replyToIds = messages.Where(m => m.ReplyToId.HasValue).Select(m => m.ReplyToId.Value).Distinct().ToList();
                var replyMessages = new Dictionary<int, (string Text, int SenderId, string? AttachmentType)>();

                if (replyToIds.Any())
                {
                    replyMessages = await _context.ChatMessages
                        .Where(m => replyToIds.Contains(m.IdMessage))
                        .Select(m => new { m.IdMessage, m.Message, m.SenderId, m.AttachmentType })
                        .ToDictionaryAsync(
                            m => m.IdMessage,
                            m => (m.Message, m.SenderId, m.AttachmentType));
                }

                // Загружаем информацию об отправителях reply-to сообщений
                var replySenderIds = replyMessages.Values.Select(r => r.SenderId).Distinct().ToList();
                var replySenders = await _context.Users
                    .Where(u => replySenderIds.Contains(u.IdUser))
                    .Select(u => new { u.IdUser, u.LastName, u.FirstName })
                    .ToDictionaryAsync(u => u.IdUser, u => $"{u.LastName} {u.FirstName}".Trim());

                // Загружаем информацию о прочитанных сообщениях
                var readMessages = await _context.ChatMessageReads
                    .Where(r => r.Message.ChatId == chatId)
                    .Select(r => new { r.MessageId, r.UserId })
                    .ToListAsync();

                // Формируем DTO
                var messageDtos = messages.Select(m => new ChatMessageDto
                {
                    Id = m.IdMessage,
                    Text = m.Message,
                    SenderId = m.SenderId,
                    SenderName = senders.ContainsKey(m.SenderId) ? senders[m.SenderId] : "Пользователь",
                    SentAt = m.SentAt,
                    EditedAt = m.EditedAt,
                    ReplyToId = m.ReplyToId,
                    ReplyTo = m.ReplyToId.HasValue && replyMessages.ContainsKey(m.ReplyToId.Value)
                        ? new ReplyMessageDto
                        {
                            Id = m.ReplyToId.Value,
                            Text = replyMessages[m.ReplyToId.Value].Text,
                            SenderId = replyMessages[m.ReplyToId.Value].SenderId,
                            SenderName = replySenders.ContainsKey(replyMessages[m.ReplyToId.Value].SenderId)
                                ? replySenders[replyMessages[m.ReplyToId.Value].SenderId]
                                : "",
                            AttachmentType = replyMessages[m.ReplyToId.Value].AttachmentType
                        }
                        : null,
                    AttachmentType = m.AttachmentType,
                    AttachmentUrl = m.AttachmentUrl,
                    AttachmentName = m.AttachmentName,
                    AttachmentSize = m.AttachmentSize,
                    IsOutgoing = m.SenderId == userId,
                    ReadBy = readMessages
                        .Where(r => r.MessageId == m.IdMessage)
                        .Select(r => r.UserId)
                        .ToList()
                }).ToList();

                _logger.LogInformation("Найдено сообщений: {Count}", messageDtos.Count);

                return Json(new ApiResponse<List<ChatMessageDto>>
                {
                    Success = true,
                    Data = messageDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сообщений чата {ChatId}", chatId);
                return Json(new ApiResponse<List<ChatMessageDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке сообщений: " + ex.Message
                });
            }
        }

        // POST: /Chats/SendMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");

                if (userId == null)
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });

                // Проверяем доступ
                var isMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == request.ChatId && cm.UserId == userId);

                if (!isMember)
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нет доступа к чату"
                    });

                // Прямой SQL запрос
                var sql = @"
            INSERT INTO [ChatMessages] 
            ([message], [sentAt], [idUser], [idChat], [replyToId], [attachmentName], [attachmentSize], [attachmentType], [attachmentUrl])
            VALUES 
            (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var parameters = new[]
                {
            new Microsoft.Data.SqlClient.SqlParameter("@p0", request.Text ?? ""),
            new Microsoft.Data.SqlClient.SqlParameter("@p1", DateTime.UtcNow),
            new Microsoft.Data.SqlClient.SqlParameter("@p2", userId.Value),
            new Microsoft.Data.SqlClient.SqlParameter("@p3", request.ChatId),
            new Microsoft.Data.SqlClient.SqlParameter("@p4", request.ReplyToId ?? (object)DBNull.Value),
            new Microsoft.Data.SqlClient.SqlParameter("@p5", request.AttachmentName ?? (object)DBNull.Value),
            new Microsoft.Data.SqlClient.SqlParameter("@p6", request.AttachmentSize ?? (object)DBNull.Value),
            new Microsoft.Data.SqlClient.SqlParameter("@p7", request.AttachmentType ?? (object)DBNull.Value),
            new Microsoft.Data.SqlClient.SqlParameter("@p8", request.AttachmentUrl ?? (object)DBNull.Value)
        };

                var messageId = await _context.Database.ExecuteSqlRawAsync(sql, parameters);

                // Обновляем время последнего сообщения
                var chat = await _context.Chats.FindAsync(request.ChatId);
                if (chat != null)
                {
                    chat.LastMessageAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        messageId = messageId,
                        text = request.Text,
                        senderId = userId.Value,
                        sentAt = DateTime.UtcNow,
                        attachmentName = request.AttachmentName,
                        attachmentUrl = request.AttachmentUrl,
                        attachmentSize = request.AttachmentSize,
                        attachmentType = request.AttachmentType
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка отправки сообщения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка сервера: " + ex.Message
                });
            }
        }

        // POST: /Chats/MarkAsRead?chatId=5
        [HttpPost]
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

        // GET: /Chats/GetNewMessages?chatId=5&lastMessageId=0
        [HttpGet]
        public async Task<IActionResult> GetNewMessages(int chatId, [FromQuery] int lastMessageId = 0)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<ChatMessageDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var isMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (!isMember)
                {
                    return Json(new ApiResponse<List<ChatMessageDto>>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этому чату"
                    });
                }

                // Загружаем новые сообщения
                var messages = await _context.ChatMessages
                    .Where(m => m.ChatId == chatId && m.IdMessage > lastMessageId)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        m.IdMessage,
                        m.Message,
                        m.SentAt,
                        m.EditedAt,
                        m.SenderId,
                        m.ReplyToId,
                        m.AttachmentType,
                        m.AttachmentUrl,
                        m.AttachmentName,
                        m.AttachmentSize
                    })
                    .ToListAsync();

                if (!messages.Any())
                {
                    return Json(new ApiResponse<List<ChatMessageDto>>
                    {
                        Success = true,
                        Data = new List<ChatMessageDto>()
                    });
                }

                // Загружаем информацию об отправителях
                var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
                var senders = await _context.Users
                    .Where(u => senderIds.Contains(u.IdUser))
                    .Select(u => new { u.IdUser, u.LastName, u.FirstName })
                    .ToDictionaryAsync(u => u.IdUser, u => $"{u.LastName} {u.FirstName}".Trim());

                // Формируем DTO
                var messageDtos = messages.Select(m => new ChatMessageDto
                {
                    Id = m.IdMessage,
                    Text = m.Message,
                    SenderId = m.SenderId,
                    SenderName = senders.ContainsKey(m.SenderId) ? senders[m.SenderId] : "Пользователь",
                    SentAt = m.SentAt,
                    EditedAt = m.EditedAt,
                    ReplyToId = m.ReplyToId,
                    AttachmentType = m.AttachmentType,
                    AttachmentUrl = m.AttachmentUrl,
                    AttachmentName = m.AttachmentName,
                    AttachmentSize = m.AttachmentSize,
                    IsOutgoing = m.SenderId == userId
                }).ToList();

                return Json(new ApiResponse<List<ChatMessageDto>>
                {
                    Success = true,
                    Data = messageDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении новых сообщений");
                return Json(new ApiResponse<List<ChatMessageDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке новых сообщений: " + ex.Message
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
                _logger.LogInformation("CreatePrivateChat вызван с otherUserId: {OtherUserId}", otherUserId);

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
                    .Include(c => c.Members)
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
                        Data = existingChat.IdChat,
                        Message = "Чат уже существует"
                    });
                }

                // Получаем информацию о пользователях
                var currentUser = await _context.Users.FindAsync(currentUserId);
                var otherUser = await _context.Users.FindAsync(otherUserId);

                if (currentUser == null || otherUser == null)
                {
                    return Json(new ApiResponse<int>
                    {
                        Success = false,
                        Message = "Пользователь не найден"
                    });
                }

                // Создаем новый чат
                var chat = new Chat
                {
                    Name = $"{currentUser.FirstName} {currentUser.LastName} & {otherUser.FirstName} {otherUser.LastName}",
                    Type = "private",
                    CreatedById = currentUserId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                // Добавляем участников
                _context.ChatMembers.Add(new ChatMember
                {
                    ChatId = chat.IdChat,
                    UserId = currentUserId.Value,
                    Role = "admin",
                    JoinedAt = DateTime.UtcNow
                });

                _context.ChatMembers.Add(new ChatMember
                {
                    ChatId = chat.IdChat,
                    UserId = otherUserId,
                    Role = "member",
                    JoinedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<int>
                {
                    Success = true,
                    Data = chat.IdChat,
                    Message = "Чат успешно создан"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании личного чата");
                return Json(new ApiResponse<int>
                {
                    Success = false,
                    Message = "Ошибка при создании чата: " + ex.Message
                });
            }
        }

        // POST: /Chats/CreateGroupChat
        [HttpPost]
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
                    IdTrip = request.TripId,
                    CreatedById = currentUserId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                // Добавляем участников
                var chatMembers = allUserIds.Select(userId => new ChatMember
                {
                    ChatId = chat.IdChat,
                    UserId = userId,
                    Role = userId == currentUserId ? "admin" : "member",
                    JoinedAt = DateTime.UtcNow
                });

                _context.ChatMembers.AddRange(chatMembers);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { chatId = chat.IdChat },
                    Message = "Чат успешно создан"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании группового чата");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при создании чата: " + ex.Message
                });
            }
        }

        // GET: /Chats/SearchUsers?term=...
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
                    .Select(u => new
                    {
                        u.IdUser,
                        u.FirstName,
                        u.LastName,
                        u.MiddleName,
                        u.Email,
                        u.AvatarPath,
                        IsFriend = _context.Friends.Any(f =>
                            f.UserId == userId && f.FriendId == u.IdUser && f.Status == "accepted"),
                        PendingSent = _context.FriendRequests.Any(r =>
                            r.SenderId == userId && r.ReceiverId == u.IdUser && r.Status == "pending"),
                        PendingReceived = _context.FriendRequests.Any(r =>
                            r.SenderId == u.IdUser && r.ReceiverId == userId && r.Status == "pending")
                    })
                    .ToListAsync();

                var result = users.Select(u => new SearchUsersResponse
                {
                    Id = u.IdUser,
                    FullName = u.LastName + " " + u.FirstName +
                        (string.IsNullOrEmpty(u.MiddleName) ? "" : " " + u.MiddleName),
                    FirstName = u.FirstName ?? "",
                    LastName = u.LastName ?? "",
                    Email = u.Email ?? "",
                    AvatarPath = u.AvatarPath,
                    IsFriend = u.IsFriend,
                    FriendStatus = u.IsFriend ? "accepted" :
                                   u.PendingSent ? "pending_sent" :
                                   u.PendingReceived ? "pending_received" : "none"
                })
                .Take(20)
                .ToList();

                return Json(new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = true,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске пользователей");
                return Json(new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = false,
                    Message = "Ошибка при поиске пользователей: " + ex.Message
                });
            }
        }

        // GET: /Chats/SearchUsersToAdd?chatId=5&term=...
        [HttpGet]
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
                        Email = u.Email ?? "",
                        AvatarPath = u.AvatarPath,
                        IsFriend = _context.Friends.Any(f => f.UserId == userId && f.FriendId == u.IdUser && f.Status == "accepted")
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
                    Message = "Ошибка при поиске пользователей: " + ex.Message
                });
            }
        }

        // POST: /Chats/AddMember
        [HttpPost]
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
                    Message = "Ошибка при добавлении участника: " + ex.Message
                });
            }
        }

        // POST: /Chats/LeaveChat?chatId=5
        [HttpPost]
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
                    Message = "Ошибка при выходе из чата: " + ex.Message
                });
            }
        }

        // GET: /Chats/GetFriendsList
        [HttpGet]
        public async Task<IActionResult> GetFriendsList()
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
                    Message = "Ошибка при загрузке друзей: " + ex.Message
                });
            }
        }

        // GET: /Chats/GetFriendsForGroup
        [HttpGet]
        public async Task<IActionResult> GetFriendsForGroup()
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
                    Message = "Ошибка при загрузке друзей: " + ex.Message
                });
            }
        }

        // GET: /Chats/GetTripParticipants?tripId=5
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
                    Message = "Ошибка при загрузке участников: " + ex.Message
                });
            }
        }
        // POST: /Chats/DeleteChat/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChat(int chatId)
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

                _logger.LogInformation("DeleteChat: chatId={ChatId}, userId={UserId}", chatId, userId);

                // Находим чат
                var chat = await _context.Chats
                    .Include(c => c.Members)
                    .FirstOrDefaultAsync(c => c.IdChat == chatId);

                if (chat == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Чат не найден"
                    });
                }

                // Проверяем права на удаление
                var isAdmin = chat.Members?.Any(m => m.UserId == userId && m.Role == "admin") ?? false;
                var isPrivateChat = chat.Type == "private";

                // Для приватных чатов: удаляем только себя
                if (isPrivateChat)
                {
                    var member = chat.Members?.FirstOrDefault(m => m.UserId == userId);
                    if (member != null)
                    {
                        _context.ChatMembers.Remove(member);
                        await _context.SaveChangesAsync();

                        // Если в чате больше нет участников, удаляем чат полностью
                        var remainingMembers = await _context.ChatMembers
                            .CountAsync(cm => cm.ChatId == chatId);

                        if (remainingMembers == 0)
                        {
                            _context.Chats.Remove(chat);
                            await _context.SaveChangesAsync();
                        }

                        return Json(new ApiResponse<object>
                        {
                            Success = true,
                            Message = "Вы покинули чат",
                            Data = new { chatDeleted = remainingMembers == 0 }
                        });
                    }
                }
                // Для групповых чатов: только админ может удалить
                else if (chat.Type == "group")
                {
                    if (!isAdmin)
                    {
                        return Json(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Только администратор может удалить групповой чат"
                        });
                    }

                    // Удаляем чат со всеми связями (каскадно удалятся сообщения и участники)
                    _context.Chats.Remove(chat);
                    await _context.SaveChangesAsync();

                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Чат успешно удален",
                        Data = new { chatDeleted = true }
                    });
                }

                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Невозможно удалить чат"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении чата {ChatId}", chatId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении чата: " + ex.Message
                });
            }
        }
        // POST: /Chats/UploadFile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<FileUploadResponse>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                if (file == null || file.Length == 0)
                {
                    return Json(new ApiResponse<FileUploadResponse>
                    {
                        Success = false,
                        Message = "Файл не выбран или пуст"
                    });
                }

                _logger.LogInformation("UploadFile: userId={UserId}, fileName={FileName}, fileSize={FileSize}, contentType={ContentType}",
                    userId, file.FileName, file.Length, file.ContentType);

                // Проверка размера файла (максимум 10 МБ)
                if (file.Length > 10 * 1024 * 1024)
                {
                    return Json(new ApiResponse<FileUploadResponse>
                    {
                        Success = false,
                        Message = "Размер файла не должен превышать 10 МБ"
                    });
                }

                // Получаем расширение файла
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                // Проверка типа файла (разрешенные расширения)
                var allowedExtensions = new[] {
            ".jpg", ".jpeg", ".png", ".gif", ".webp",  // изображения
            ".pdf",                                     // документы
            ".doc", ".docx",                            // Word
            ".xls", ".xlsx",                             // Excel
            ".txt",                                      // текст
            ".zip", ".rar", ".7z"                        // архивы
        };

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new ApiResponse<FileUploadResponse>
                    {
                        Success = false,
                        Message = "Недопустимый тип файла. Разрешены: " + string.Join(", ", allowedExtensions)
                    });
                }

                // Определяем КОРОТКИЙ тип файла по расширению (максимум 20 символов)
                string fileType = fileExtension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".pdf" => "application/pdf",
                    ".doc" => "application/msword",
                    ".docx" => "application/docx",  // Короткий вариант
                    ".xls" => "application/xls",
                    ".xlsx" => "application/xlsx",   // Короткий вариант
                    ".txt" => "text/plain",
                    ".zip" => "application/zip",
                    ".rar" => "application/rar",
                    ".7z" => "application/7z",
                    _ => "application/octet-stream"
                };

                // Создаем уникальное имя файла
                var fileName = $"{Guid.NewGuid()}{fileExtension}";

                // Путь для сохранения (папка Uploads в wwwroot)
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");

                // Создаем папку, если её нет
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    _logger.LogInformation("Created upload directory: {UploadsFolder}", uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);
                _logger.LogInformation("Saving file to: {FilePath}", filePath);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Проверяем, что файл действительно создан
                if (!System.IO.File.Exists(filePath))
                {
                    throw new Exception("Файл не был сохранен");
                }

                // Формируем URL для доступа к файлу
                var fileUrl = $"/uploads/chat/{fileName}";

                var response = new FileUploadResponse
                {
                    FileName = file.FileName,
                    FileUrl = fileUrl,
                    FileSize = file.Length,
                    FileType = fileType  // Используем КОРОТКИЙ тип
                };

                _logger.LogInformation("File uploaded successfully: {FileName}, URL: {FileUrl}, Type: {FileType}",
                    file.FileName, fileUrl, fileType);

                return Json(new ApiResponse<FileUploadResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке файла");
                return Json(new ApiResponse<FileUploadResponse>
                {
                    Success = false,
                    Message = "Ошибка при загрузке файла: " + ex.Message
                });
            }
        }
        // POST: /Chats/RenameChat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameChat([FromBody] RenameChatRequest request)
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

                _logger.LogInformation("RenameChat: chatId={ChatId}, userId={UserId}, newName={NewName}",
                    request.ChatId, userId, request.NewName);

                // Проверяем, что новое название не пустое
                if (string.IsNullOrWhiteSpace(request.NewName))
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Название чата не может быть пустым"
                    });
                }

                // Проверяем длину названия
                if (request.NewName.Length > 200)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Название чата не должно превышать 200 символов"
                    });
                }

                // Находим чат
                var chat = await _context.Chats
                    .Include(c => c.Members)
                    .FirstOrDefaultAsync(c => c.IdChat == request.ChatId);

                if (chat == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Чат не найден"
                    });
                }

                // Проверяем, является ли пользователь участником чата
                var isMember = chat.Members?.Any(m => m.UserId == userId) ?? false;
                if (!isMember)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Вы не являетесь участником этого чата"
                    });
                }

                // Разрешаем переименовывать любые чаты, кроме системных
                // Если это групповой чат, проверяем права администратора
                if (chat.Type == "group")
                {
                    var isAdmin = chat.Members?.Any(m => m.UserId == userId && m.Role == "admin") ?? false;
                    if (!isAdmin)
                    {
                        return Json(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Только администратор может переименовать групповой чат"
                        });
                    }
                }
                // Для приватных чатов разрешаем переименовывать обоим участникам
                else if (chat.Type == "private")
                {
                    // В приватном чате любой участник может переименовать
                    _logger.LogInformation("Private chat rename allowed for user {UserId}", userId);
                }

                // Сохраняем старое название для логирования
                var oldName = chat.Name;

                // Обновляем название
                chat.Name = request.NewName.Trim();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Chat renamed successfully: ChatId={ChatId}, OldName={OldName}, NewName={NewName}",
                    chat.IdChat, oldName, chat.Name);

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Название чата успешно изменено",
                    Data = new { newName = chat.Name }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переименовании чата");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при переименовании чата: " + ex.Message
                });
            }
        }
    }
}