using BocaAPI.Models.DTO;

namespace BocaAPI.Interfaces
{
    public interface IBocaRepository
    {
        Task<List<PoliceCode>> GetPoliceCodes();
        Task <IEnumerable<RawExportData>>  UploadToDatabase(List<VCSExport> records, string FileName);
        Task<IEnumerable<RawExportData>> GetForOutput();
        Task<List<Error>> GetErrors();
        Task  DeleteErrors();
        void LogError(Error er);
    }
}
