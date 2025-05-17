using System.Collections.Generic;
using System.Threading.Tasks;
using DOMRIA.Models;

namespace DOMRIA.Interfaces
{
    public interface IUserStateService
    {
        Task<List<UserSearchState>> GetAllAsync();
        Task<UserSearchState?> GetAsync(long userId);
        Task<bool> SaveAsync(UserSearchState state);
        Task<UserSearchState> GetOrCreateUserStateAsync(long userId);
        Task<bool> DeleteAsync(long userId);
    }
}
