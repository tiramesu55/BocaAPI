using Boca.API.Entities;

namespace BocaAPI.Repository
{
    public interface IHoursRepository
    {
        IEnumerable<SourceTime> GetHoursAsync();
    }
}
