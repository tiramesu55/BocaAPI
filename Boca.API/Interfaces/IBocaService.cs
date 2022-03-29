namespace BocaAPI.Interfaces
{
    public interface IBocaService
    {
        Task UploadInputFileToDatabase();
        ICacheService Cache { get; }
        IBocaRepository Repository { get; }
    }
}
