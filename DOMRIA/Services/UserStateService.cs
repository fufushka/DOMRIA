using DOMRIA.Interfaces;
using DOMRIA.Models;
using MongoDB.Driver;

namespace DOMRIA.Services
{
    public class UserStateService : IUserStateService
    {
        private readonly IMongoCollection<UserSearchState> _collection;

        public UserStateService(IMongoDatabase database)
        {
            _collection = database.GetCollection<UserSearchState>("user_states");
        }

        public async Task<List<UserSearchState>> GetAllAsync()
        {
            try
            {
                var filter = Builders<UserSearchState>.Filter.Empty;
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MongoDB] GetAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveAsync(UserSearchState state)
        {
            try
            {
                await _collection.ReplaceOneAsync(
                    s => s.UserId == state.UserId,
                    state,
                    new ReplaceOptions { IsUpsert = true }
                );
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MongoDB] SaveAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<UserSearchState?> GetAsync(long userId)
        {
            try
            {
                return await _collection.Find(s => s.UserId == userId).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MongoDB] GetAsync error: {ex.Message}");
                throw;
            }
        }

        public async Task<UserSearchState> GetOrCreateUserStateAsync(long userId)
        {
            try
            {
                var existing = await GetAsync(userId);
                if (existing != null)
                    return existing;

                var newState = new UserSearchState { UserId = userId };
                await SaveAsync(newState);
                return newState;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MongoDB] GetAsync error: {ex.Message}");
                return null;
            }
        }

        //public async Task<bool> DeleteAsync(long userId)
        //{
        //    try
        //    {
        //        var result = await _collection.DeleteOneAsync(s => s.UserId == userId);
        //        return result.DeletedCount > 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ [MongoDB] DeleteAsync error: {ex.Message}");
        //        return false;
        //    }
        //}
    }
}
