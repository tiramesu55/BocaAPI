using BocaAPI.Models.DTO;

namespace BocaAPI.Interfaces
{
    public interface IBocaService
    {
        Task UploadInputFileToDatabase();
        Task<List<FinalResult>> ExportLatest(string InsertId, string FileName = "VCSTime");
        IBocaRepository Repository { get; }
        IEmail Email { get; }

        //3. Post for returning all ids in date range (startDt - endDt)
    }
}
