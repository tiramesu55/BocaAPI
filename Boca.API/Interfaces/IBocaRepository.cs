using BocaAPI.Models.DTO;

namespace BocaAPI.Interfaces
{
    public interface IBocaRepository
    {
        Task<List<PoliceCode>> GetPoliceCodes();
        Task<List<VCSExport>> UploadToDatabase(List<VCSExport> records);
    }
}
