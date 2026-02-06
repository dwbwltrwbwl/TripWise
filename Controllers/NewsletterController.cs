using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Text.RegularExpressions;

namespace TripWise.Controllers
{
    public class NewsletterController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<NewsletterController> _logger;
        private readonly EmailService _emailService;

        public NewsletterController(TripWiseContext context,
            ILogger<NewsletterController> logger,
            EmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        // POST: /Newsletter/Subscribe
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(string email)
        {
            try
            {
                _logger.LogInformation($"Subscribe attempt for email: {email}");

                // Валидация email
                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Empty email provided");
                    return Json(new { success = false, message = "Введите email адрес" });
                }

                if (!IsValidEmail(email))
                {
                    _logger.LogWarning($"Invalid email format: {email}");
                    return Json(new { success = false, message = "Введите корректный email" });
                }

                // ПРОСТОЕ РЕШЕНИЕ: Пока сохраняем только в лог
                // Позже добавим в БД
                _logger.LogInformation($"NEW SUBSCRIPTION: {email}");

                // Отправляем приветственное письмо
                try
                {
                    await SendWelcomeEmail(email);
                    _logger.LogInformation($"Welcome email sent to: {email}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, $"Failed to send welcome email to: {email}");
                    // Не прерываем подписку из-за ошибки email
                }

                return Json(new
                {
                    success = true,
                    message = "Вы успешно подписались на рассылку! Проверьте ваш email."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подписке на рассылку");
                return Json(new
                {
                    success = false,
                    message = $"Произошла ошибка при подписке: {ex.Message}"
                });
            }
        }

        private async Task SendWelcomeEmail(string email)
        {
            var subject = "Добро пожаловать в рассылку Вместе В Путь! 🎉";
            var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h2 style='color: #0379D9;'>Спасибо за подписку!</h2>
                </div>
                
                <div style='background: #f8f9fa; padding: 20px; border-radius: 10px; margin-bottom: 20px;'>
                    <h3 style='color: #333; margin-top: 0;'>Что вас ждет?</h3>
                    <ul style='color: #555; line-height: 1.6; padding-left: 20px;'>
                        <li>🔥 Лучшие предложения на авиабилеты и отели</li>
                        <li>📅 Уведомления о скидках и акциях</li>
                        <li>🗺️ Полезные советы для путешественников</li>
                        <li>👥 Идеи для групповых поездок</li>
                    </ul>
                </div>
                
                <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                    <p style='color: #888; font-size: 12px;'>
                        С уважением, команда <strong>Вместе В Путь</strong><br>
                        {DateTime.Now.Year} © Все права защищены
                    </p>
                </div>
            </div>";

            await _emailService.SendAsync(email, subject, body);
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}