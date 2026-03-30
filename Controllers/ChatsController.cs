using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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
                        c.AvatarPath, // ДОБАВЬТЕ ЭТУ СТРОКУ

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
                    AvatarPath = c.AvatarPath, // ДОБАВЬТЕ ЭТУ СТРОКУ

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
                    AvatarPath = chat.AvatarPath,
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

                // Загружаем все сообщения
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
                        m.AttachmentSize,
                        m.AttachmentsJson
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
                var replyMessages = new Dictionary<int, (string Text, int SenderId, string? AttachmentType, string? AttachmentsJson)>();

                if (replyToIds.Any())
                {
                    replyMessages = await _context.ChatMessages
                        .Where(m => replyToIds.Contains(m.IdMessage))
                        .Select(m => new { m.IdMessage, m.Message, m.SenderId, m.AttachmentType, m.AttachmentsJson })
                        .ToDictionaryAsync(
                            m => m.IdMessage,
                            m => (m.Message, m.SenderId, m.AttachmentType, m.AttachmentsJson));
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
                // В GetChatMessages, при формировании messageDtos
                var messageDtos = messages.Select(m =>
                {
                    // Десериализуем вложения из JSON
                    List<AttachmentDto>? attachments = null;
                    if (!string.IsNullOrEmpty(m.AttachmentsJson))
                    {
                        try
                        {
                            attachments = System.Text.Json.JsonSerializer.Deserialize<List<AttachmentDto>>(m.AttachmentsJson);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка десериализации attachments для сообщения {0}", m.IdMessage);
                        }
                    }

                    // Для голосований - это могут быть данные голосования, а не вложения
                    // Поэтому проверяем тип
                    bool isVote = m.AttachmentType == "vote";

                    return new ChatMessageDto
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
                        Attachments = attachments,
                        IsVote = isVote,  // Добавьте это поле в DTO
                        VoteDataJson = isVote ? m.AttachmentsJson : null,  // Сохраняем JSON для голосования
                        IsOutgoing = m.SenderId == userId
                    };
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

        // POST: /Chats/SendMessage - версия с прямым SQL
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

                _logger.LogInformation("SendMessage: chatId={ChatId}, userId={UserId}, text={Text}, attachmentsCount={AttachmentsCount}",
                    request.ChatId, userId, request.Text, request.Attachments?.Count ?? 0);

                // Проверяем доступ
                var isMember = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == request.ChatId && cm.UserId == userId);

                if (!isMember)
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нет доступа к чату"
                    });

                // Сериализуем attachments в JSON
                string? attachmentsJson = null;
                if (request.Attachments != null && request.Attachments.Any())
                {
                    attachmentsJson = System.Text.Json.JsonSerializer.Serialize(request.Attachments);
                }

                // Для обратной совместимости - первый файл (если есть)
                var firstAttachment = request.Attachments?.FirstOrDefault();

                // Прямой SQL запрос с OUTPUT
                var sql = @"
    DECLARE @InsertedIds TABLE (Id INT);
    
    INSERT INTO [ChatMessages] 
    ([message], [sentAt], [idUser], [idChat], [replyToId], 
     [attachmentName], [attachmentSize], [attachmentType], [attachmentUrl], [attachmentsJson])
    OUTPUT INSERTED.idMessage INTO @InsertedIds
    VALUES 
    (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9);
    
    SELECT Id FROM @InsertedIds;";

                var parameters = new[]
                {
            new SqlParameter("@p0", request.Text ?? ""),
            new SqlParameter("@p1", DateTime.UtcNow),
            new SqlParameter("@p2", userId.Value),
            new SqlParameter("@p3", request.ChatId),
            new SqlParameter("@p4", request.ReplyToId ?? (object)DBNull.Value),
            new SqlParameter("@p5", firstAttachment?.FileName ?? (object)DBNull.Value),
            new SqlParameter("@p6", firstAttachment?.FileSize ?? (object)DBNull.Value),
            new SqlParameter("@p7", firstAttachment?.FileType ?? (object)DBNull.Value),
            new SqlParameter("@p8", firstAttachment?.FileUrl ?? (object)DBNull.Value),
            new SqlParameter("@p9", attachmentsJson ?? (object)DBNull.Value)
        };

                // Выполняем SQL и получаем результат
                int messageId = 0;
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);

                    await _context.Database.OpenConnectionAsync();
                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        messageId = Convert.ToInt32(result);
                    }
                }

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
                        sentAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc), // Явно указываем UTC
                        attachments = request.Attachments
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
                        m.AttachmentSize,
                        m.AttachmentsJson
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
                var messageDtos = messages.Select(m =>
                {
                    // Десериализуем вложения из JSON
                    List<AttachmentDto>? attachments = null;
                    if (!string.IsNullOrEmpty(m.AttachmentsJson))
                    {
                        try
                        {
                            attachments = System.Text.Json.JsonSerializer.Deserialize<List<AttachmentDto>>(m.AttachmentsJson);
                        }
                        catch { }
                    }

                    // Для обратной совместимости
                    if ((attachments == null || attachments.Count == 0) && !string.IsNullOrEmpty(m.AttachmentUrl))
                    {
                        attachments = new List<AttachmentDto>
                {
                    new AttachmentDto
                    {
                        FileName = m.AttachmentName ?? "Файл",
                        FileUrl = m.AttachmentUrl,
                        FileSize = m.AttachmentSize ?? 0,
                        FileType = m.AttachmentType ?? "application/octet-stream"
                    }
                };
                    }

                    return new ChatMessageDto
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
                        Attachments = attachments,
                        IsOutgoing = m.SenderId == userId
                    };
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
        // POST: /Chats/DeleteMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage([FromBody] DeleteMessageRequest request)
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

                _logger.LogInformation("DeleteMessage: messageId={MessageId}, userId={UserId}", request.MessageId, userId);

                // Проверяем существование сообщения и права через прямой SQL запрос с возвратом результата
                var checkSql = @"
    SELECT COUNT(*) 
    FROM [ChatMessages] 
    WHERE [idMessage] = @messageId AND [idUser] = @userId";

                int exists = 0;
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = checkSql;
                    command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));
                    command.Parameters.Add(new SqlParameter("@userId", userId.Value));

                    await _context.Database.OpenConnectionAsync();
                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        exists = Convert.ToInt32(result);
                    }
                }

                if (exists == 0)
                {
                    _logger.LogWarning("Сообщение {MessageId} не найдено или пользователь {UserId} не является автором",
                        request.MessageId, userId);
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Сообщение не найдено или вы не являетесь его автором"
                    });
                }

                // Получаем chatId до удаления сообщения
                var getChatIdSql = "SELECT [idChat] FROM [ChatMessages] WHERE [idMessage] = @messageId";
                int chatId = 0;
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = getChatIdSql;
                    command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));

                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        chatId = Convert.ToInt32(result);
                    }
                }

                // СНИМАЕМ ГЛОБАЛЬНОЕ ЗАКРЕПЛЕНИЕ, если сообщение закреплено для всех
                var updateChatSql = @"
    UPDATE [Chats] 
    SET [pinnedMessageId] = NULL, 
        [pinnedAt] = NULL, 
        [pinnedById] = NULL 
    WHERE [idChat] = @chatId AND [pinnedMessageId] = @messageId";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = updateChatSql;
                    command.Parameters.Add(new SqlParameter("@chatId", chatId));
                    command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));
                    await command.ExecuteNonQueryAsync();
                }

                // УДАЛЯЕМ ЛИЧНЫЕ ЗАКРЕПЛЕНИЯ этого сообщения у всех пользователей
                var deleteUserPinsSql = @"
    DELETE FROM [UserPinnedMessages] 
    WHERE [messageId] = @messageId";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = deleteUserPinsSql;
                    command.Parameters.Clear();
                    command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));
                    await command.ExecuteNonQueryAsync();
                }

                // Удаляем сообщение
                var deleteSql = "DELETE FROM [ChatMessages] WHERE [idMessage] = @messageId";
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = deleteSql;
                    command.Parameters.Clear();
                    command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));
                    await command.ExecuteNonQueryAsync();
                }

                // Обновляем время последнего сообщения в чате
                if (chatId > 0)
                {
                    var updateLastMessageSql = @"
        UPDATE [Chats] 
        SET [lastMessageAt] = (
            SELECT MAX([sentAt]) 
            FROM [ChatMessages] 
            WHERE [idChat] = @chatId
        )
        WHERE [idChat] = @chatId";

                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = updateLastMessageSql;
                        command.Parameters.Clear();
                        command.Parameters.Add(new SqlParameter("@chatId", chatId));
                        await command.ExecuteNonQueryAsync();
                    }
                }

                _logger.LogInformation("Message deleted successfully: MessageId={MessageId}", request.MessageId);

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Сообщение удалено",
                    Data = new { chatId }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении сообщения {MessageId}", request?.MessageId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении сообщения: " + ex.Message
                });
            }
        }
        // POST: /Chats/PinMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PinMessage([FromBody] PinMessageRequest request)
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

                _logger.LogInformation("PinMessage: messageId={MessageId}, userId={UserId}, pinForAll={PinForAll}",
                    request.MessageId, userId, request.PinForAll);

                // Проверяем существование сообщения и получаем chatId
                int chatId = 0;
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "SELECT [idChat] FROM [ChatMessages] WHERE [idMessage] = @messageId";
                    command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));

                    await _context.Database.OpenConnectionAsync();
                    try
                    {
                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            chatId = Convert.ToInt32(result);
                        }
                    }
                    finally
                    {
                        await _context.Database.CloseConnectionAsync();
                    }
                }

                if (chatId == 0)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Сообщение не найдено"
                    });
                }

                if (request.PinForAll)
                {
                    // Закрепление для всех (только для администраторов)

                    // Проверяем права администратора
                    int isAdmin = 0;
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM [ChatMembers] WHERE [idChat] = @chatId AND [idUser] = @userId AND [role] = 'admin'";
                        command.Parameters.Add(new SqlParameter("@chatId", chatId));
                        command.Parameters.Add(new SqlParameter("@userId", userId.Value));

                        await _context.Database.OpenConnectionAsync();
                        try
                        {
                            var result = await command.ExecuteScalarAsync();
                            if (result != null)
                            {
                                isAdmin = Convert.ToInt32(result);
                            }
                        }
                        finally
                        {
                            await _context.Database.CloseConnectionAsync();
                        }
                    }

                    if (isAdmin == 0)
                    {
                        return Json(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Только администратор может закреплять сообщения для всех"
                        });
                    }

                    // Обновляем чат - закрепляем сообщение для всех
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "UPDATE [Chats] SET [pinnedMessageId] = @messageId, [pinnedAt] = @pinnedAt, [pinnedById] = @userId WHERE [idChat] = @chatId";
                        command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));
                        command.Parameters.Add(new SqlParameter("@pinnedAt", DateTime.UtcNow));
                        command.Parameters.Add(new SqlParameter("@userId", userId.Value));
                        command.Parameters.Add(new SqlParameter("@chatId", chatId));

                        await _context.Database.OpenConnectionAsync();
                        try
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                        finally
                        {
                            await _context.Database.CloseConnectionAsync();
                        }
                    }

                    _logger.LogInformation("Message pinned for all: MessageId={MessageId}, ChatId={ChatId}",
                        request.MessageId, chatId);

                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Сообщение закреплено для всех",
                        Data = new
                        {
                            messageId = request.MessageId,
                            pinnedAt = DateTime.UtcNow,
                            pinnedBy = userId,
                            pinForAll = true
                        }
                    });
                }
                else
                {
                    // Личное закрепление (доступно всем участникам)

                    // Проверяем, не закреплено ли уже это сообщение пользователем
                    int existingPin = 0;
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM [UserPinnedMessages] WHERE [userId] = @userId AND [chatId] = @chatId";
                        command.Parameters.Add(new SqlParameter("@userId", userId.Value));
                        command.Parameters.Add(new SqlParameter("@chatId", chatId));

                        await _context.Database.OpenConnectionAsync();
                        try
                        {
                            var result = await command.ExecuteScalarAsync();
                            if (result != null)
                            {
                                existingPin = Convert.ToInt32(result);
                            }
                        }
                        finally
                        {
                            await _context.Database.CloseConnectionAsync();
                        }
                    }

                    if (existingPin > 0)
                    {
                        // Обновляем существующее закрепление
                        using (var command = _context.Database.GetDbConnection().CreateCommand())
                        {
                            command.CommandText = "UPDATE [UserPinnedMessages] SET [messageId] = @messageId, [pinnedAt] = @pinnedAt WHERE [userId] = @userId AND [chatId] = @chatId";
                            command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));
                            command.Parameters.Add(new SqlParameter("@pinnedAt", DateTime.UtcNow));
                            command.Parameters.Add(new SqlParameter("@userId", userId.Value));
                            command.Parameters.Add(new SqlParameter("@chatId", chatId));

                            await _context.Database.OpenConnectionAsync();
                            try
                            {
                                await command.ExecuteNonQueryAsync();
                            }
                            finally
                            {
                                await _context.Database.CloseConnectionAsync();
                            }
                        }
                    }
                    else
                    {
                        // Создаем новое закрепление
                        using (var command = _context.Database.GetDbConnection().CreateCommand())
                        {
                            command.CommandText = "INSERT INTO [UserPinnedMessages] ([userId], [chatId], [messageId], [pinnedAt]) VALUES (@userId, @chatId, @messageId, @pinnedAt)";
                            command.Parameters.Add(new SqlParameter("@userId", userId.Value));
                            command.Parameters.Add(new SqlParameter("@chatId", chatId));
                            command.Parameters.Add(new SqlParameter("@messageId", request.MessageId));
                            command.Parameters.Add(new SqlParameter("@pinnedAt", DateTime.UtcNow));

                            await _context.Database.OpenConnectionAsync();
                            try
                            {
                                await command.ExecuteNonQueryAsync();
                            }
                            finally
                            {
                                await _context.Database.CloseConnectionAsync();
                            }
                        }
                    }

                    _logger.LogInformation("Message pinned for user: MessageId={MessageId}, UserId={UserId}",
                        request.MessageId, userId);

                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Сообщение закреплено в вашем чате",
                        Data = new
                        {
                            messageId = request.MessageId,
                            pinnedAt = DateTime.UtcNow,
                            pinnedBy = userId,
                            pinForAll = false
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при закреплении сообщения {MessageId}", request?.MessageId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при закреплении сообщения: " + ex.Message
                });
            }
        }

        // POST: /Chats/UnpinMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnpinMessage([FromBody] UnpinMessageRequest request)
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

                _logger.LogInformation("UnpinMessage: chatId={ChatId}, userId={UserId}, pinForAll={PinForAll}",
                    request.ChatId, userId, request.PinForAll);

                if (request.PinForAll)
                {
                    // Открепление для всех (только для администраторов)

                    // Проверяем права администратора
                    int isAdmin = 0;
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM [ChatMembers] WHERE [idChat] = @chatId AND [idUser] = @userId AND [role] = 'admin'";
                        command.Parameters.Add(new SqlParameter("@chatId", request.ChatId));
                        command.Parameters.Add(new SqlParameter("@userId", userId.Value));

                        await _context.Database.OpenConnectionAsync();
                        try
                        {
                            var result = await command.ExecuteScalarAsync();
                            if (result != null)
                            {
                                isAdmin = Convert.ToInt32(result);
                            }
                        }
                        finally
                        {
                            await _context.Database.CloseConnectionAsync();
                        }
                    }

                    if (isAdmin == 0)
                    {
                        return Json(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Только администратор может откреплять сообщения для всех"
                        });
                    }

                    // Открепляем сообщение для всех
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "UPDATE [Chats] SET [pinnedMessageId] = NULL, [pinnedAt] = NULL, [pinnedById] = NULL WHERE [idChat] = @chatId";
                        command.Parameters.Add(new SqlParameter("@chatId", request.ChatId));

                        await _context.Database.OpenConnectionAsync();
                        try
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                        finally
                        {
                            await _context.Database.CloseConnectionAsync();
                        }
                    }

                    _logger.LogInformation("Message unpinned for all: ChatId={ChatId}", request.ChatId);
                }
                else
                {
                    // Личное открепление (доступно всем)
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = "DELETE FROM [UserPinnedMessages] WHERE [userId] = @userId AND [chatId] = @chatId";
                        command.Parameters.Add(new SqlParameter("@userId", userId.Value));
                        command.Parameters.Add(new SqlParameter("@chatId", request.ChatId));

                        await _context.Database.OpenConnectionAsync();
                        try
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                        finally
                        {
                            await _context.Database.CloseConnectionAsync();
                        }
                    }

                    _logger.LogInformation("Message unpinned for user: UserId={UserId}, ChatId={ChatId}",
                        userId, request.ChatId);
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = request.PinForAll ? "Сообщение откреплено для всех" : "Сообщение откреплено"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при откреплении сообщения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при откреплении сообщения: " + ex.Message
                });
            }
        }

        // GET: /Chats/GetPinnedMessage?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetPinnedMessage(int chatId)
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

                var sql = @"
            SELECT 
                c.pinnedMessageId,
                c.pinnedAt,
                c.pinnedById,
                m.[idMessage],
                m.[message],
                m.[sentAt],
                m.[idUser] as SenderId,
                m.[attachmentType],
                m.[attachmentUrl],
                m.[attachmentName],
                u.[last_name] as SenderLastName,
                u.[first_name] as SenderFirstName,
                pu.[last_name] as PinnedByLastName,
                pu.[first_name] as PinnedByFirstName
            FROM [Chats] c
            LEFT JOIN [ChatMessages] m ON c.pinnedMessageId = m.[idMessage]
            LEFT JOIN [Users] u ON m.[idUser] = u.[idUser]
            LEFT JOIN [Users] pu ON c.pinnedById = pu.[idUser]
            WHERE c.[idChat] = @chatId AND c.pinnedMessageId IS NOT NULL";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.Add(new SqlParameter("@chatId", chatId));

                    await _context.Database.OpenConnectionAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var pinnedMessage = new
                            {
                                id = reader["pinnedMessageId"],
                                text = reader["message"] as string ?? "",
                                senderId = reader["SenderId"],
                                senderName = (reader["SenderLastName"] as string ?? "") + " " + (reader["SenderFirstName"] as string ?? ""),
                                sentAt = reader["sentAt"],
                                pinnedAt = reader["pinnedAt"],
                                pinnedBy = (reader["PinnedByLastName"] as string ?? "") + " " + (reader["PinnedByFirstName"] as string ?? ""),
                                attachmentType = reader["attachmentType"] as string,
                                attachmentUrl = reader["attachmentUrl"] as string,
                                attachmentName = reader["attachmentName"] as string
                            };

                            return Json(new ApiResponse<object>
                            {
                                Success = true,
                                Data = pinnedMessage
                            });
                        }
                    }
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении закрепленного сообщения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке закрепленного сообщения: " + ex.Message
                });
            }
        }
        // GET: /Chats/GetUserPinnedMessage?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetUserPinnedMessage(int chatId)
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

                var sql = @"
            SELECT 
                up.[messageId],
                up.[pinnedAt],
                m.[idMessage],
                m.[message],
                m.[sentAt],
                m.[idUser] as SenderId,
                m.[attachmentType],
                m.[attachmentUrl],
                m.[attachmentName],
                u.[last_name] as SenderLastName,
                u.[first_name] as SenderFirstName
            FROM [UserPinnedMessages] up
            INNER JOIN [ChatMessages] m ON up.[messageId] = m.[idMessage]
            LEFT JOIN [Users] u ON m.[idUser] = u.[idUser]
            WHERE up.[chatId] = @chatId AND up.[userId] = @userId";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.Add(new SqlParameter("@chatId", chatId));
                    command.Parameters.Add(new SqlParameter("@userId", userId.Value));

                    await _context.Database.OpenConnectionAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var pinnedMessage = new
                            {
                                id = reader["messageId"],
                                text = reader["message"] as string ?? "",
                                senderId = reader["SenderId"],
                                senderName = (reader["SenderLastName"] as string ?? "") + " " + (reader["SenderFirstName"] as string ?? ""),
                                sentAt = reader["sentAt"],
                                pinnedAt = reader["pinnedAt"],
                                pinnedBy = "Вы",
                                attachmentType = reader["attachmentType"] as string,
                                attachmentUrl = reader["attachmentUrl"] as string,
                                attachmentName = reader["attachmentName"] as string
                            };

                            return Json(new ApiResponse<object>
                            {
                                Success = true,
                                Data = pinnedMessage
                            });
                        }
                    }
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении личного закрепленного сообщения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке личного закрепленного сообщения: " + ex.Message
                });
            }
        }
        // POST: /Chats/UploadChatAvatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadChatAvatar(IFormFile file, [FromQuery] int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("UploadChatAvatar: chatId={ChatId}, userId={UserId}, fileName={FileName}",
                    chatId, userId, file?.FileName);

                if (file == null || file.Length == 0)
                {
                    return Json(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Файл не выбран или пуст"
                    });
                }

                // Проверяем права (только администратор может менять аватар чата)
                var isAdmin = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId && cm.Role == "admin");

                if (!isAdmin)
                {
                    return Json(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Только администратор может изменять аватар чата"
                    });
                }

                // Проверка размера файла (максимум 5 МБ для аватаров)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return Json(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Размер файла не должен превышать 5 МБ"
                    });
                }

                // Проверка типа файла (только изображения)
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Недопустимый тип файла. Разрешены: " + string.Join(", ", allowedExtensions)
                    });
                }

                // Создаем уникальное имя файла
                var fileName = $"chat_{chatId}_{Guid.NewGuid()}{fileExtension}";

                // Путь для сохранения
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat_avatars");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);

                // Удаляем старый аватар, если есть
                var chat = await _context.Chats.FindAsync(chatId);
                if (chat != null && !string.IsNullOrEmpty(chat.AvatarPath))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", chat.AvatarPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Сохраняем новый файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Формируем URL для доступа к файлу
                var avatarUrl = $"/uploads/chat_avatars/{fileName}";

                // Обновляем путь в базе данных
                if (chat != null)
                {
                    chat.AvatarPath = avatarUrl;
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Chat avatar uploaded successfully: ChatId={ChatId}, URL={AvatarUrl}",
                    chatId, avatarUrl);

                return Json(new ApiResponse<string>
                {
                    Success = true,
                    Data = avatarUrl,
                    Message = "Аватар чата успешно обновлен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке аватара чата");
                return Json(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Ошибка при загрузке аватара: " + ex.Message
                });
            }
        }

        // POST: /Chats/DeleteChatAvatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChatAvatar([FromBody] DeleteChatAvatarRequest request)
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

                _logger.LogInformation("DeleteChatAvatar: chatId={ChatId}, userId={UserId}", request.ChatId, userId);

                // Проверяем права (только администратор может удалить аватар)
                var isAdmin = await _context.ChatMembers
                    .AnyAsync(cm => cm.ChatId == request.ChatId && cm.UserId == userId && cm.Role == "admin");

                if (!isAdmin)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только администратор может удалять аватар чата"
                    });
                }

                var chat = await _context.Chats.FindAsync(request.ChatId);
                if (chat == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Чат не найден"
                    });
                }

                // Удаляем файл, если есть
                if (!string.IsNullOrEmpty(chat.AvatarPath))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", chat.AvatarPath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Обновляем базу данных
                chat.AvatarPath = null;
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Аватар чата удален"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении аватара чата");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении аватара: " + ex.Message
                });
            }
        }
        // GET: /Chats/CheckMessagePinned?messageId=123
        [HttpGet]
        public async Task<IActionResult> CheckMessagePinned(int messageId)
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

                _logger.LogInformation("CheckMessagePinned: messageId={MessageId}, userId={UserId}", messageId, userId);

                // Проверяем, является ли сообщение глобальным закрепом
                var globalPin = await _context.Chats
                    .AnyAsync(c => c.PinnedMessageId == messageId);

                // Проверяем, является ли сообщение личным закрепом для этого пользователя
                var userPin = await _context.UserPinnedMessages
                    .AnyAsync(up => up.MessageId == messageId && up.UserId == userId);

                // Проверяем, есть ли личные закрепы у других пользователей
                var otherUserPins = await _context.UserPinnedMessages
                    .AnyAsync(up => up.MessageId == messageId && up.UserId != userId);

                string pinType = "none";
                if (globalPin && userPin)
                {
                    pinType = "both";
                }
                else if (globalPin)
                {
                    pinType = "global";
                }
                else if (userPin)
                {
                    pinType = "user";
                }
                else if (otherUserPins)
                {
                    // Если сообщение закреплено у других пользователей, но не у текущего
                    pinType = "other_users";
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        isPinned = globalPin || userPin || otherUserPins,
                        pinType = pinType,
                        hasGlobalPin = globalPin,
                        hasUserPin = userPin,
                        hasOtherUserPins = otherUserPins
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке закрепления сообщения {MessageId}", messageId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при проверке закрепления: " + ex.Message
                });
            }
        }
        // GET: /Chats/GetVotes?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetVotes(int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<VoteDto>>
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
                    return Json(new ApiResponse<List<VoteDto>>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этому чату"
                    });
                }

                // Используем сырой SQL для получения голосований
                var sql = @"
            SELECT 
                v.IdVote,
                v.question,
                v.createdAt,
                v.expiresAt,
                v.idTrip,
                v.createdById,
                v.idPoint,
                v.idChat,
                u.last_name as CreatorLastName,
                u.first_name as CreatorFirstName,
                t.title as TripTitle
            FROM [votingSystems] v
            LEFT JOIN [Users] u ON v.createdById = u.idUser
            LEFT JOIN [Trips] t ON v.idTrip = t.idTrip
            WHERE v.idChat = @chatId
            ORDER BY v.createdAt DESC";

                var votes = new List<VotingSystemDto>();

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.Add(new SqlParameter("@chatId", chatId));

                    if (_context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                    {
                        await _context.Database.OpenConnectionAsync();
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var vote = new VotingSystemDto
                            {
                                IdVote = reader.GetInt32(0),
                                Question = reader.GetString(1),
                                CreatedAt = reader.GetDateTime(2),
                                ExpiresAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                                IdTrip = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                                CreatedById = reader.GetInt32(5),
                                IdPoint = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                                IdChat = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                                CreatorName = !reader.IsDBNull(8) && !reader.IsDBNull(9)
                                    ? $"{reader.GetString(8)} {reader.GetString(9)}".Trim()
                                    : "Система",
                                TripName = reader.IsDBNull(10) ? null : reader.GetString(10)
                            };
                            votes.Add(vote);
                        }
                    }
                }

                // Для каждого голосования получаем варианты и голоса
                var voteDtos = new List<VoteDto>();

                foreach (var vote in votes)
                {
                    // Получаем варианты ответов
                    var optionsSql = @"
                SELECT o.idVoteOption, o.optionText
                FROM [VoteOptions] o
                WHERE o.idVote = @voteId";

                    var options = new List<VoteOptionDto>();

                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = optionsSql;
                        command.Parameters.Clear();
                        command.Parameters.Add(new SqlParameter("@voteId", vote.IdVote));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                options.Add(new VoteOptionDto
                                {
                                    Id = reader.GetInt32(0),
                                    Text = reader.GetString(1),
                                    VotesCount = 0,
                                    TotalVotes = 0,
                                    VoterIds = new List<int>()
                                });
                            }
                        }
                    }

                    // Получаем голоса для каждого варианта
                    foreach (var option in options)
                    {
                        var votesSql = @"
                    SELECT uv.idUser
                    FROM [UserVotes] uv
                    WHERE uv.idVoteOption = @optionId";

                        using (var command = _context.Database.GetDbConnection().CreateCommand())
                        {
                            command.CommandText = votesSql;
                            command.Parameters.Clear();
                            command.Parameters.Add(new SqlParameter("@optionId", option.Id));

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    option.VotesCount++;
                                    option.VoterIds.Add(reader.GetInt32(0));
                                }
                            }
                        }
                    }

                    var totalVotes = options.Sum(o => o.VotesCount);
                    foreach (var opt in options)
                    {
                        opt.TotalVotes = totalVotes;
                    }

                    var userHasVoted = options.Any(o => o.VoterIds.Contains(userId.Value));
                    var userVoteOptionId = options
                        .Where(o => o.VoterIds.Contains(userId.Value))
                        .Select(o => (int?)o.Id)
                        .FirstOrDefault();

                    voteDtos.Add(new VoteDto
                    {
                        Id = vote.IdVote,
                        Question = vote.Question,
                        CreatedAt = vote.CreatedAt,
                        ExpiresAt = vote.ExpiresAt,
                        CreatedById = vote.CreatedById,
                        CreatedByName = vote.CreatorName,
                        TripId = vote.IdTrip,
                        TripName = vote.TripName,
                        PointId = vote.IdPoint,
                        ChatId = vote.IdChat,
                        Options = options,
                        TotalVotes = totalVotes,
                        UserHasVoted = userHasVoted,
                        UserVoteOptionId = userVoteOptionId,
                        IsExpired = vote.ExpiresAt.HasValue && vote.ExpiresAt.Value < DateTime.UtcNow
                    });
                }

                return Json(new ApiResponse<List<VoteDto>>
                {
                    Success = true,
                    Data = voteDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении голосований");
                return Json(new ApiResponse<List<VoteDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке голосований: " + ex.Message
                });
            }
        }

        // POST: /Chats/CreateVote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVote([FromBody] CreateVoteRequest request)
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

                // Проверяем, что запрос не пустой
                if (request == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Неверный запрос"
                    });
                }

                // Проверяем обязательные поля
                if (string.IsNullOrWhiteSpace(request.Question))
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Введите вопрос"
                    });
                }

                if (request.Options == null || request.Options.Count < 2)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Добавьте минимум 2 варианта ответа"
                    });
                }

                // Проверяем, существует ли чат
                var chatExists = await _context.Chats
                    .AnyAsync(c => c.IdChat == request.ChatId);

                if (!chatExists)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Чат с ID {request.ChatId} не найден"
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
                        Message = "Вы не являетесь участником этого чата"
                    });
                }

                // Проверяем, что варианты не пустые
                var validOptions = request.Options.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
                if (validOptions.Count < 2)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Все варианты ответа должны быть заполнены"
                    });
                }

                int voteId = 0;

                // Создаем голосование через SQL
                var connection = _context.Database.GetDbConnection();

                try
                {
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync();
                    }

                    // Вставляем голосование
                    var insertVoteSql = @"
                INSERT INTO [votingSystems] 
                ([question], [createdAt], [expiresAt], [idTrip], [createdById], [idPoint], [idChat])
                VALUES 
                (@question, @createdAt, @expiresAt, @idTrip, @createdById, @idPoint, @idChat);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = insertVoteSql;
                        command.Parameters.Add(new SqlParameter("@question", request.Question.Trim()));
                        command.Parameters.Add(new SqlParameter("@createdAt", DateTime.UtcNow));
                        command.Parameters.Add(new SqlParameter("@expiresAt", request.ExpiresAt?.ToUniversalTime() ?? (object)DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@idTrip", DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@createdById", userId.Value));
                        command.Parameters.Add(new SqlParameter("@idPoint", DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@idChat", request.ChatId));

                        var result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            voteId = Convert.ToInt32(result);
                        }
                    }

                    if (voteId == 0)
                    {
                        return Json(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Не удалось создать голосование"
                        });
                    }

                    // Добавляем варианты ответов
                    foreach (var optionText in validOptions)
                    {
                        var insertOptionSql = @"
                    INSERT INTO [VoteOptions] 
                    ([optionText], [idVote])
                    VALUES 
                    (@optionText, @voteId)";

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = insertOptionSql;
                            command.Parameters.Clear();
                            command.Parameters.Add(new SqlParameter("@optionText", optionText.Trim()));
                            command.Parameters.Add(new SqlParameter("@voteId", voteId));
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    // Создаем данные голосования для attachmentsJson
                    var voteData = new
                    {
                        voteId = voteId,
                        question = request.Question,
                        options = validOptions,
                        optionsCount = validOptions.Count,
                        expiresAt = request.ExpiresAt?.ToUniversalTime()
                    };

                    var attachmentsJson = System.Text.Json.JsonSerializer.Serialize(voteData);

                    // ОТПРАВЛЯЕМ СООБЩЕНИЕ В ЧАТ С ПРАВИЛЬНЫМИ ПОЛЯМИ
                    var insertMessageSql = @"
                INSERT INTO [ChatMessages] 
                ([message], [sentAt], [idUser], [idChat], [attachmentType], [attachmentsJson])
                VALUES 
                (@message, @sentAt, @senderId, @chatId, @attachmentType, @attachmentsJson);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    int messageId = 0;

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = insertMessageSql;
                        command.Parameters.Clear();
                        // Важно: message должен быть пустым или содержать только текст для отображения
                        // Но attachmentType = "vote" и attachmentsJson содержат данные голосования
                        command.Parameters.Add(new SqlParameter("@message", "")); // Пустое сообщение, голосование будет отображаться через attachmentType
                        command.Parameters.Add(new SqlParameter("@sentAt", DateTime.UtcNow));
                        command.Parameters.Add(new SqlParameter("@senderId", userId.Value));
                        command.Parameters.Add(new SqlParameter("@chatId", request.ChatId));
                        command.Parameters.Add(new SqlParameter("@attachmentType", "vote")); // ВАЖНО: устанавливаем тип "vote"
                        command.Parameters.Add(new SqlParameter("@attachmentsJson", attachmentsJson));

                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            messageId = Convert.ToInt32(result);
                        }
                    }

                    // Обновляем время последнего сообщения
                    var chat = await _context.Chats.FindAsync(request.ChatId);
                    if (chat != null)
                    {
                        chat.LastMessageAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }

                    _logger.LogInformation("Голосование создано: VoteId={VoteId}, MessageId={MessageId}, ChatId={ChatId}",
                        voteId, messageId, request.ChatId);

                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Голосование создано",
                        Data = new
                        {
                            voteId = voteId,
                            messageId = messageId
                        }
                    });
                }
                finally
                {
                    // Не закрываем соединение, оно управляется контекстом
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL ошибка при создании голосования: {Message}", ex.Message);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка базы данных: " + ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании голосования");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка: " + ex.Message
                });
            }
        }

        // POST: /Chats/SubmitVote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitVote([FromBody] SubmitVoteRequest request)
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

                // Проверяем существование варианта и не истекло ли голосование
                var checkSql = @"
            SELECT v.IdVote, v.expiresAt 
            FROM [VoteOptions] o
            INNER JOIN [votingSystems] v ON o.idVote = v.IdVote
            WHERE o.idVoteOption = @optionId";

                int voteId = 0;
                DateTime? expiresAt = null;

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = checkSql;
                    command.Parameters.Add(new SqlParameter("@optionId", request.OptionId));

                    await _context.Database.OpenConnectionAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            voteId = reader.GetInt32(0);
                            expiresAt = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                        }
                    }
                }

                if (voteId == 0)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Вариант ответа не найден"
                    });
                }

                // Проверяем, не истекло ли голосование
                if (expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Голосование завершено"
                    });
                }

                // Проверяем, голосовал ли уже пользователь
                var checkVoteSql = @"
            SELECT COUNT(*) FROM [UserVotes] 
            WHERE idVoteOption = @optionId AND idUser = @userId";

                int existingVoteCount = 0;
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = checkVoteSql;
                    command.Parameters.Add(new SqlParameter("@optionId", request.OptionId));
                    command.Parameters.Add(new SqlParameter("@userId", userId.Value));

                    var result = await command.ExecuteScalarAsync();
                    existingVoteCount = result != null ? Convert.ToInt32(result) : 0;
                }

                // Если голосовал - удаляем старый голос
                if (existingVoteCount > 0)
                {
                    var deleteSql = "DELETE FROM [UserVotes] WHERE idVoteOption = @optionId AND idUser = @userId";
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = deleteSql;
                        command.Parameters.Add(new SqlParameter("@optionId", request.OptionId));
                        command.Parameters.Add(new SqlParameter("@userId", userId.Value));
                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Добавляем новый голос
                var insertSql = @"
            INSERT INTO [UserVotes] 
            ([idVoteOption], [idUser], [votedAt])
            VALUES 
            (@optionId, @userId, @votedAt)";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = insertSql;
                    command.Parameters.Add(new SqlParameter("@optionId", request.OptionId));
                    command.Parameters.Add(new SqlParameter("@userId", userId.Value));
                    command.Parameters.Add(new SqlParameter("@votedAt", DateTime.UtcNow));
                    await command.ExecuteNonQueryAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Ваш голос учтен"
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL ошибка при голосовании");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка базы данных: " + ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при голосовании");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка: " + ex.Message
                });
            }
        }
    }
    public class VotingSystemDto
    {
        public int IdVote { get; set; }
        public string Question { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? IdTrip { get; set; }
        public int CreatedById { get; set; }
        public int? IdPoint { get; set; }
        public int? IdChat { get; set; }
        public string CreatorName { get; set; } = "";
        public string? TripName { get; set; }
    }
}