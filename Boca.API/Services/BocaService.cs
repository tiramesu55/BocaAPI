using BocaAPI.Extensions;
using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace BocaAPI.Services
{
    public class BocaService: ServiceBase, IBocaService
    {
        private readonly ICacheService _cacheService;
        private readonly IBocaRepository _repository;
        
        public BocaService(ICacheService cacheService, IBocaRepository repository, ILoggerService logger): base(logger)
        {
            _cacheService = cacheService;
            _repository = repository;
        }

        public async Task UploadInputFileToDatabase() => await ExecuteOperationAsync(async () =>
        {
            //TODO: Stub, needs normal configuration
            string inputFileName = "";
            string outputFileName = "";

            var records = CsvExtensions.ReadFromCsv<VCSExport>(File.OpenRead(inputFileName));

            var policeCodes = await _cacheService.GetPoliceCodes();

            //TODO: Validation
            var inserted = await _repository.UploadToDatabase(records);

            File.WriteAllBytes(outputFileName, CsvExtensions.SaveToCSV(inserted));

        });
        

    }
}
