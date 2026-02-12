using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace TripWise.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(TripWiseContext context, IWebHostEnvironment environment, ILogger<DocumentsController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        // GET: /Account/MyDocuments
        public IActionResult MyDocuments()
        {
            return View();
        }

        // GET: /Documents/GetUserFolders
        [HttpGet]
        public async Task<IActionResult> GetUserFolders()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            var folders = await _context.DocumentFolders
                .Where(f => f.UserId == userId)
                .Select(f => new
                {
                    f.IdFolder,
                    f.Name,
                    f.Description,
                    f.Color,
                    DocumentCount = f.Documents.Count
                })
                .OrderBy(f => f.Name)
                .ToListAsync();

            return Json(folders);
        }

        // POST: /Documents/CreateFolder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                // Проверяем, нет ли уже папки с таким именем
                var existingFolder = await _context.DocumentFolders
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.Name == request.Name);

                if (existingFolder != null)
                    return Json(new { success = false, message = "Папка с таким именем уже существует" });

                var folder = new DocumentFolder
                {
                    Name = request.Name,
                    Description = request.Description,
                    Color = request.Color,
                    UserId = userId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DocumentFolders.Add(folder);
                await _context.SaveChangesAsync();

                return Json(new { success = true, folderId = folder.IdFolder });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании папки");
                return Json(new { success = false, message = "Ошибка при создании папки" });
            }
        }

        // GET: /Documents/GetUserDocuments
        [HttpGet]
        public async Task<IActionResult> GetUserDocuments(int? folderId, string search = "", string filterType = "", string sortBy = "newest")
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized();

                var query = _context.UserDocuments
                    .Where(d => d.UserId == userId)
                    .Include(d => d.Folder)
                    .AsQueryable();

                // Фильтрация по папке
                if (folderId.HasValue && folderId > 0)
                {
                    query = query.Where(d => d.FolderId == folderId);
                }

                // Поиск по названию
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(d => d.Name.Contains(search) ||
                                           d.Description.Contains(search) ||
                                           d.DocumentNumber.Contains(search));
                }

                // Фильтрация по типу файла
                if (!string.IsNullOrEmpty(filterType))
                {
                    switch (filterType.ToLower())
                    {
                        case "pdf":
                            query = query.Where(d => d.FileType.ToLower() == ".pdf");
                            break;
                        case "doc":
                            query = query.Where(d => d.FileType.ToLower() == ".doc" || d.FileType.ToLower() == ".docx");
                            break;
                        case "image":
                            query = query.Where(d => d.FileType.ToLower() == ".jpg" ||
                                                   d.FileType.ToLower() == ".jpeg" ||
                                                   d.FileType.ToLower() == ".png" ||
                                                   d.FileType.ToLower() == ".gif");
                            break;
                    }
                }

                // Сортировка
                switch (sortBy.ToLower())
                {
                    case "oldest":
                        query = query.OrderBy(d => d.CreatedAt);
                        break;
                    case "name_asc":
                        query = query.OrderBy(d => d.Name);
                        break;
                    case "name_desc":
                        query = query.OrderByDescending(d => d.Name);
                        break;
                    case "size_asc":
                        query = query.OrderBy(d => d.FileSize);
                        break;
                    case "size_desc":
                        query = query.OrderByDescending(d => d.FileSize);
                        break;
                    default: // newest
                        query = query.OrderByDescending(d => d.CreatedAt);
                        break;
                }

                var documents = await query
                    .Select(d => new
                    {
                        d.IdDocument,
                        d.Name,
                        d.Description,
                        d.FileType,
                        d.FileSize,
                        d.FilePath,
                        d.DocumentType,
                        d.DocumentNumber,
                        d.DocumentDate,
                        d.CreatedAt,
                        FolderId = d.FolderId,
                        FolderName = d.Folder != null ? d.Folder.Name : null
                    })
                    .ToListAsync();

                return Json(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении документов");
                return StatusCode(500, new { error = "Ошибка при получении документов" });
            }
        }

        // POST: /Documents/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                if (request.File == null || request.File.Length == 0)
                    return Json(new { success = false, message = "Файл не выбран" });

                // Проверяем размер файла (10MB максимум)
                if (request.File.Length > 10 * 1024 * 1024)
                    return Json(new { success = false, message = "Размер файла не должен превышать 10MB" });

                // Проверяем расширение файла
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".txt", ".xls", ".xlsx" };
                var fileExtension = Path.GetExtension(request.File.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                    return Json(new { success = false, message = "Недопустимый тип файла" });

                // Создаем директорию для документов пользователя, если ее нет
                var userFolder = Path.Combine(_environment.WebRootPath, "documents", userId.ToString());
                if (!Directory.Exists(userFolder))
                    Directory.CreateDirectory(userFolder);

                // Генерируем уникальное имя файла
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = Path.Combine(userFolder, fileName);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var document = new UserDocument  // ИЗМЕНЕНО: Document -> UserDocument
                {
                    Name = request.Name,
                    Description = request.Description,
                    FileType = fileExtension,
                    FileSize = request.File.Length,
                    FilePath = $"/documents/{userId}/{fileName}",
                    DocumentType = request.DocumentType,
                    DocumentNumber = request.DocumentNumber,
                    DocumentDate = request.DocumentDate,
                    FolderId = request.FolderId > 0 ? request.FolderId : null,
                    UserId = userId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserDocuments.Add(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true, documentId = document.IdDocument });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке документа");
                return Json(new { success = false, message = "Ошибка при загрузке документа" });
            }
        }

        // GET: /Documents/GetDocument/{id}
        [HttpGet]
        public async Task<IActionResult> GetDocument(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized();

                var document = await _context.UserDocuments
                    .Include(d => d.Folder)
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return NotFound(new { success = false, message = "Документ не найден" });

                return Json(new
                {
                    success = true,
                    idDocument = document.IdDocument,
                    name = document.Name,
                    description = document.Description,
                    fileType = document.FileType,
                    fileSize = document.FileSize,
                    filePath = document.FilePath,
                    documentType = document.DocumentType,
                    documentNumber = document.DocumentNumber,
                    documentDate = document.DocumentDate,
                    createdAt = document.CreatedAt,
                    folderName = document.Folder?.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении документа {DocumentId}", id);
                return StatusCode(500, new { success = false, message = "Ошибка при получении документа" });
            }
        }

        // GET: /Documents/Download/{id}
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized();

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return NotFound();

                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                var memory = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                return File(memory, GetContentType(document.FileType), $"{document.Name}{document.FileType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при скачивании документа");
                return StatusCode(500);
            }
        }

        // GET: /Documents/GetFile/{id} (для превью изображений)
        [HttpGet]
        public async Task<IActionResult> GetFile(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized();

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return NotFound();

                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                return PhysicalFile(filePath, GetContentType(document.FileType));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении файла");
                return StatusCode(500);
            }
        }

        // DELETE: /Documents/DeleteDocument/{id}
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return Json(new { success = false, message = "Документ не найден" });

                // Удаляем физический файл
                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Удаляем запись из базы данных
                _context.UserDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении документа");
                return Json(new { success = false, message = "Ошибка при удалении документа" });
            }
        }

        // Вспомогательный метод для определения Content-Type
        private string GetContentType(string fileType)
        {
            return fileType.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".txt" => "text/plain",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream",
            };
        }
    }

    // Модели запросов
    public class CreateFolderRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
    }

    public class UploadDocumentRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IFormFile File { get; set; }
        public int? FolderId { get; set; }
        public string DocumentType { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime? DocumentDate { get; set; }
    }
}