using BocaAPI.Extensions;
using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Models.DTO;
using BocaAPI.Validators;
using Microsoft.Extensions.Options;

namespace BocaAPI.Services
{
    public class BocaService : ServiceBase, IBocaService
    {
        private readonly ICacheService _cacheService;
        private readonly IBocaRepository _repository;
        private readonly Settings _settings;

        public BocaService(ICacheService cacheService, IBocaRepository repository, ILoggerService logger, IOptions<Settings> options) : base(logger)
        {
            _cacheService = cacheService;
            _repository = repository;
            _settings = options.Value;
        }

        public async Task UploadInputFileToDatabase() => await ExecuteOperationAsync(async () =>
        {
            foreach (var file in Directory.GetFiles(_settings.InputFilePath, "*.csv"))
            {
                var fileName = Path.GetFileName(file);

                var validator = new PoliceMasterValidator();

                var records = CsvExtensions.ReadFromCsv<VCSExport>(File.OpenRead(file));

                var policeCodes = await _cacheService.GetPoliceCodes();

                var validatedRecords = records.Select((r, i) =>
                {
                    var validationResult = validator.Validate(r);
                    return new { Number = i, Record = r, IsValid = validationResult.IsValid, Errors = validationResult.Errors.Select(e => e.ErrorMessage).StringJoin() };
                }).ToList();

                validatedRecords.Where(r => !r.IsValid).ToList().ForEach(r => _logger.LogInfo(r.Number, r.Errors));

                var inserted = await _repository.UploadToDatabase(validatedRecords.Where(r => r.IsValid).Select(r => r.Record).ToList());

                var finalResults = inserted.Select(r => (FinalResult)r).ToList();

                File.WriteAllBytes(Path.Combine(_settings.OutputFilePath, $"{fileName}_processed"), CsvExtensions.SaveToCSV(finalResults));

                File.Delete(file);
            }
        });


    }
}
