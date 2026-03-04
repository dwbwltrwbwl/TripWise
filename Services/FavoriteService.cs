using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Text.Json;

namespace TripWise.Services
{
    public interface IFavoriteService
    {
        // Для авиабилетов
        Task<bool> AddFavoriteFlightAsync(FavoriteFlight favorite);
        Task<bool> RemoveFavoriteFlightAsync(int userId, string flightId);
        Task<List<FavoriteFlight>> GetUserFavoriteFlightsAsync(int userId);
        Task<bool> IsFlightInFavoritesAsync(int userId, string flightId);

        // Для ЖД билетов
        Task<bool> AddFavoriteTrainAsync(FavoriteTrain favorite);
        Task<bool> RemoveFavoriteTrainAsync(int userId, string trainGroupId);
        Task<List<FavoriteTrain>> GetUserFavoriteTrainsAsync(int userId);
        Task<bool> IsTrainInFavoritesAsync(int userId, string trainGroupId);
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

        #region Авиабилеты

        public async Task<bool> AddFavoriteFlightAsync(FavoriteFlight favorite)
        {
            try
            {
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
                return await _context.FavoriteFlights
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.AddedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранных рейсов для пользователя {UserId}", userId);
                return new List<FavoriteFlight>();
            }
        }

        public async Task<bool> IsFlightInFavoritesAsync(int userId, string flightId)
        {
            try
            {
                if (string.IsNullOrEmpty(flightId))
                    return false;

                return await _context.FavoriteFlights
                    .AnyAsync(f => f.UserId == userId && f.FlightId == flightId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке рейса {FlightId} в избранном", flightId);
                return false;
            }
        }

        #endregion

        #region ЖД билеты

        public async Task<bool> AddFavoriteTrainAsync(FavoriteTrain favorite)
        {
            try
            {
                var existing = await _context.FavoriteTrains
                    .FirstOrDefaultAsync(f => f.UserId == favorite.UserId && f.TrainGroupId == favorite.TrainGroupId);

                if (existing != null)
                {
                    _logger.LogInformation("Поезд {TrainGroupId} уже в избранном у пользователя {UserId}",
                        favorite.TrainGroupId, favorite.UserId);
                    return false;
                }

                _context.FavoriteTrains.Add(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Поезд {TrainGroupId} добавлен в избранное для пользователя {UserId}",
                    favorite.TrainGroupId, favorite.UserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении поезда {TrainGroupId} в избранное", favorite.TrainGroupId);
                return false;
            }
        }

        public async Task<bool> RemoveFavoriteTrainAsync(int userId, string trainGroupId)
        {
            try
            {
                var favorite = await _context.FavoriteTrains
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.TrainGroupId == trainGroupId);

                if (favorite == null)
                {
                    _logger.LogWarning("Поезд {TrainGroupId} не найден в избранном у пользователя {UserId}",
                        trainGroupId, userId);
                    return false;
                }

                _context.FavoriteTrains.Remove(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Поезд {TrainGroupId} удален из избранного для пользователя {UserId}",
                    trainGroupId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении поезда {TrainGroupId} из избранного", trainGroupId);
                return false;
            }
        }

        public async Task<List<FavoriteTrain>> GetUserFavoriteTrainsAsync(int userId)
        {
            try
            {
                return await _context.FavoriteTrains
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.AddedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранных поездов для пользователя {UserId}", userId);
                return new List<FavoriteTrain>();
            }
        }

        public async Task<bool> IsTrainInFavoritesAsync(int userId, string trainGroupId)
        {
            try
            {
                if (string.IsNullOrEmpty(trainGroupId))
                    return false;

                return await _context.FavoriteTrains
                    .AnyAsync(f => f.UserId == userId && f.TrainGroupId == trainGroupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке поезда {TrainGroupId} в избранном", trainGroupId);
                return false;
            }
        }

        #endregion
    }
}