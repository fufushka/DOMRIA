using System.Collections.Generic;
using System.Threading.Tasks;
using DOMRIA.Models;

namespace DOMRIA.Interfaces
{
    public interface IDomRiaService
    {
        Task<List<DistrictDto>> GetDistrictsAsync();
        Task<FlatSearchResponse> SearchFlatsByCriteriaAsync(UserSearchState state);
        Task<FlatResult?> GetFlatByIdAsync(int id);
    }
}
