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
                    _logger.LogInformation("Рейс {FlightId} уже в избранном у пользователя {UserId}",
                        favorite.FlightId, favorite.UserId);
                    return false;
                }

                _context.FavoriteFlights.Add(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Рейс {FlightId} добавлен в избранное для пользователя {UserId}",
                    favorite.FlightId, favorite.UserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении рейса {FlightId} в избранное", favorite.FlightId);
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
                {
                    _logger.LogWarning("Рейс {FlightId} не найден в избранном у пользователя {UserId}",
                        flightId, userId);
                    return false;
                }

                _context.FavoriteFlights.Remove(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Рейс {FlightId} удален из избранного для пользователя {UserId}",
                    flightId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении рейса {FlightId} из избранного", flightId);
                return false;
            }
        }

        public async Task<List<FavoriteFlight>> GetUserFavoriteFlightsAsync(int userId)
        {
            try
            {
                _logger.LogInformation("=== FavoriteService.GetUserFavoriteFlightsAsync ===");
                _logger.LogInformation("Поиск избранного для пользователя {UserId}", userId);

                // Сначала проверим, есть ли вообще записи в таблице для этого пользователя
                var any = await _context.FavoriteFlights.AnyAsync(f => f.UserId == userId);
                _logger.LogInformation("Есть ли записи в таблице для пользователя {UserId}? {Any}", userId, any);

                if (!any)
                {
                    _logger.LogInformation("Нет записей для пользователя {UserId}", userId);
                    return new List<FavoriteFlight>();
                }

                var favorites = await _context.FavoriteFlights
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.AddedDate)
                    .ToListAsync();

                _logger.LogInformation("Найдено {Count} рейсов в БД", favorites.Count);

                // Логируем все записи
                foreach (var flight in favorites)
                {
                    _logger.LogInformation("Рейс: ID={FlightId}, {Airline} {FlightNumber}, {DepartureCity}→{ArrivalCity}, Цена={Price}",
                        flight.FlightId, flight.Airline, flight.FlightNumber, flight.DepartureCity, flight.ArrivalCity, flight.Price);
                }

                return favorites;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при получении избранных рейсов для пользователя {UserId}", userId);
                return new List<FavoriteFlight>();
            }
        }

        public async Task<bool> IsFlightInFavoritesAsync(int userId, string flightId)
        {
            try
            {
                if (string.IsNullOrEmpty(flightId))
                {
                    _logger.LogWarning("flightId пустой или null для пользователя {UserId}", userId);
                    return false;
                }

                var exists = await _context.FavoriteFlights
                    .AnyAsync(f => f.UserId == userId && f.FlightId == flightId);

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке рейса {FlightId} в избранном", flightId);
                return false;
            }
        }
    }
}