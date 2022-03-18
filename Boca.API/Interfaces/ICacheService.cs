using BocaAPI.Models.DTO;

namespace BocaAPI.Interfaces
{
    public interface ICacheService
    {
        Task<List<PoliceCode>> GetPoliceCodes();
    }
}
