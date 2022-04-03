using BocaAPI.Models.DTO;

namespace BocaAPI.Interfaces
{
    public interface IBocaService
    {
        Task<List<RawExportData>> UploadInputFileToDatabase();
        Task<List<FinalResult>> ExportLatest();
        ICacheService Cache { get; }
        IBocaRepository Repository { get; }
        //1. get all errors
        //2. delete all errors
        //3. Post for returning all ids in date range (startDt - endDt)
    }
}
