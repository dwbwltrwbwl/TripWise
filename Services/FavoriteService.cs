// Services/FavoriteService.cs
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Text.Json;

namespace TripWise.Services
{
    public interface IFavoriteService
    {
        Task<bool> AddFavoriteFlightAsync(FavoriteFlight favorite);
        Task<bool> RemoveFavoriteFlightAsync(int userId, string flightId);
        Task<List<FavoriteFlight>> GetUserFavoriteFlightsAsync(int userId);
        Task<bool> IsFlightInFavoritesAsync(int userId, string flightId);
    }

    public class FavoriteService : IFavoriteService
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<FavoriteService> _logger;

        public FavoriteService(TripWiseContext context, ILogger<FavoriteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> AddFavoriteFlightAsync(FavoriteFlight favorite)
        {
            try
            {
                // Проверяем, не добавлен ли уже этот рейс
                var existing = await _context.FavoriteFlights
                    .FirstOrDefaultAsync(f => f.UserId == favorite.UserId && f.FlightId == favorite.FlightId);

                if (existing != null)
                {
                    return false; // Уже в избранном
                }

                _context.FavoriteFlights.Add(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Рейс {FlightId} добавлен в избранное для пользователя {UserId}",
                    favorite.FlightId, favorite.UserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении рейса в избранное");
                return false;
            }
        }

        public async Task<bool> RemoveFavoriteFlightAsync(int userId, string flightId)
        {
            try
            {
                var favorite = await _context.FavoriteFlights
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.FlightId == flightId);

                if (favorite == null)
                    return false;

                _context.FavoriteFlights.Remove(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Рейс {FlightId} удален из избранного для пользователя {UserId}",
                    flightId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении рейса из избранного");
                return false;
            }
        }

        public async Task<List<FavoriteFlight>> GetUserFavoriteFlightsAsync(int userId)
        {
            try
            {
                return await _context.FavoriteFlights
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.AddedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранных рейсов");
                return new List<FavoriteFlight>();
            }
        }

        public async Task<bool> IsFlightInFavoritesAsync(int userId, string flightId)
        {
            try
            {
                return await _context.FavoriteFlights
                    .AnyAsync(f => f.UserId == userId && f.FlightId == flightId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке рейса в избранном");
                return false;
            }
        }
    }
}