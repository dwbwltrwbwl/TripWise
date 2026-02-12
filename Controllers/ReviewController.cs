using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TripWise.Models;
using Microsoft.AspNetCore.Authorization;

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : Controller
    {
        private readonly TripWiseContext _context;

        public ReviewController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: Review/Reviews
        [HttpGet]
        public async Task<IActionResult> Reviews()
        {
            ViewData["Title"] = "Отзывы";

            var userId = HttpContext.Session.GetInt32("UserId");
            ViewBag.IsAuthenticated = userId.HasValue;

            if (userId.HasValue)
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user != null)
                {
                    ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
                    ViewBag.UserEmail = user.Email;
                }
            }

            return View();
        }

        // GET: api/Review/GetAll
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllReviews()
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new ReviewDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Email = r.Email,
                        Rating = r.Rating,
                        Text = r.Text,
                        CreatedAt = r.CreatedAt
                    })
                    .ToListAsync();

                return Ok(reviews);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при получении отзывов", details = ex.Message });
            }
        }

        // GET: api/Review/GetStatistics
        [HttpGet("GetStatistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .ToListAsync();

                var statistics = new ReviewStatisticsDto
                {
                    TotalReviews = reviews.Count,
                    AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0,
                    RatingCounts = new Dictionary<int, int>
                    {
                        { 5, reviews.Count(r => r.Rating == 5) },
                        { 4, reviews.Count(r => r.Rating == 4) },
                        { 3, reviews.Count(r => r.Rating == 3) },
                        { 2, reviews.Count(r => r.Rating == 2) },
                        { 1, reviews.Count(r => r.Rating == 1) }
                    }
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при получении статистики", details = ex.Message });
            }
        }

        // POST: api/Review/Create
        [HttpPost("Create")]
        [Authorize]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto reviewDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "Необходимо авторизоваться" });
                }

                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    return NotFound(new { error = "Пользователь не найден" });
                }

                // Проверяем, не оставлял ли пользователь уже отзыв сегодня
                var today = DateTime.UtcNow.Date;
                var existingReviewToday = await _context.Reviews
                    .AnyAsync(r => r.UserId == userId.Value &&
                                  r.CreatedAt.Date == today &&
                                  !r.IsDeleted);

                if (existingReviewToday)
                {
                    return BadRequest(new { error = "Вы уже оставляли отзыв сегодня. Пожалуйста, попробуйте завтра." });
                }

                var review = new Review
                {
                    UserId = userId.Value,
                    Name = reviewDto.Name,
                    Email = reviewDto.Email,
                    Rating = reviewDto.Rating,
                    Text = reviewDto.Text,
                    CreatedAt = DateTime.UtcNow,
                    IsApproved = true,
                    IsDeleted = false
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                var reviewDto_response = new ReviewDto
                {
                    Id = review.Id,
                    Name = review.Name,
                    Email = review.Email,
                    Rating = review.Rating,
                    Text = review.Text,
                    CreatedAt = review.CreatedAt
                };

                return Ok(new
                {
                    success = true,
                    message = "Отзыв успешно добавлен",
                    review = reviewDto_response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при сохранении отзыва", details = ex.Message });
            }
        }

        // DELETE: api/Review/Delete/5
        [HttpDelete("Delete/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "Необходимо авторизоваться" });
                }

                var review = await _context.Reviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound(new { error = "Отзыв не найден" });
                }
                if (review.UserId != userId.Value && !await IsUserAdmin(userId.Value))
                {
                    return Forbid();
                }

                review.IsDeleted = true;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Отзыв успешно удален" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при удалении отзыва", details = ex.Message });
            }
        }

        // Альтернативный метод удаления с Join для лучшей производительности
        [HttpDelete("DeleteAlt/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteReviewAlt(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "Необходимо авторизоваться" });
                }

                var review = await _context.Reviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound(new { error = "Отзыв не найден" });
                }

                // АЛЬТЕРНАТИВНЫЙ ВАРИАНТ: Используем Join для получения пользователя с ролью
                var userWithRole = await (from u in _context.Users
                                          join r in _context.Roles on u.IdRole equals r.IdRole
                                          where u.IdUser == userId.Value
                                          select new
                                          {
                                              User = u,
                                              RoleName = r.Name
                                          }).FirstOrDefaultAsync();

                if (userWithRole == null)
                {
                    return NotFound(new { error = "Пользователь не найден" });
                }

                // Проверяем, является ли пользователь автором отзыва или администратором
                if (review.UserId != userId.Value && userWithRole.RoleName != "Admin")
                {
                    return Forbid();
                }

                review.IsDeleted = true;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Отзыв успешно удален" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при удалении отзыва", details = ex.Message });
            }
        }
        private async Task<bool> IsUserAdmin(int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.IdUser == userId);

            if (user == null)
                return false;

            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.IdRole == user.IdRole);

            return role?.Name == "Admin";
        }
    }
}