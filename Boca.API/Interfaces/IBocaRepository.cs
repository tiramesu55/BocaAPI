using BocaAPI.Models.DTO;

namespace BocaAPI.Interfaces
{
    public interface IBocaRepository
    {
        Task<List<PoliceCode>> GetPoliceCodes();
        //Inserted = rows added under InsertId; Failed = records the database rejected (logged to ErrorLogs)
        Task<(IEnumerable<RawExportData> Inserted, int Failed)> UploadToDatabase(List<VCSExport> records, string FileName, string InsertId);
        Task<IEnumerable<RawExportData>> GetForOutput(string InsertId);
        Task<List<Error>> GetErrors();
        Task  DeleteErrors();
        void LogError(Error er);
        Task Archive();
    }
}
