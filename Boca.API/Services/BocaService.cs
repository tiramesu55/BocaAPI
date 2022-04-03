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
        public IBocaRepository Repository{get { return _repository; }}
        public ICacheService Cache { get { return _cacheService; } }

        public async Task<List<RawExportData>> UploadInputFileToDatabase()
        {
            var inputFolder = $@"{ _settings.BaseFilePath}\{_settings.InputFilePath}";
            var outputFolder = $@"{ _settings.BaseFilePath}\{_settings.OutputFilePath}";
            var archiveFolder = $@"{ _settings.BaseFilePath}\{_settings.ArchiveFilePath}";
            foreach (var file in Directory.GetFiles(inputFolder, "*.csv"))
            {
                var fileName = Path.GetFileName(file);

                var policeCodes = await _cacheService.GetPoliceCodes();

                var infiniumCodes = policeCodes.Select(p => p.Infinium_Codes).ToList();

                var validator = new PoliceMasterValidator(infiniumCodes);

                var readResults = File.OpenRead(file).ReadFromCsv<VCSExport>();
                readResults.Where(readResult => !readResult.IsValid)
                           .ToList()
                           .ForEach(readResult => _logger.LogInfo(readResult.RowNumber.Value, readResult.Errors));

                var records = readResults.Where(record => record.IsValid)
                                         .Select(r => r.Record)
                                         .ToList();
                var validatedRecords = records.Select((record, i) =>
                {
                    var validationResult = validator.Validate(record);
                    return new
                    {
                        Number = i, 
                        Record = record, 
                        IsValid = validationResult.IsValid, 
                        Errors = validationResult.Errors
                                                 .Select(e => e.ErrorMessage)
                                                 .StringJoin()
                    };
                }).ToList();

                var invalidRecords = validatedRecords.Where(record => !record.IsValid).ToList();
                validatedRecords.Where(record => !record.IsValid)
                                .ToList()
                                .ForEach(r => _logger.LogInfo(r.Number, r.Errors));

                var rtn = await _repository.UploadToDatabase(validatedRecords.Where(r => r.IsValid).Select(r => r.Record).ToList());
                result.AddRange(rtn.ToList());
                
                //var finalResults = inserted.Select(r => (FinalResult)r).ToList();
                //below is a separate call in a different controller
                // File.WriteAllBytes(Path.Combine(_settings.OutputFilePath, $"{fileName}_processed"), CsvExtensions.SaveToCSV(finalResults));

                File.Move(file, $@"{ArchiveFolder}\{fileName}", true);  //move with overwrite
            }
            return result;
        }
        public async Task<List<FinalResult>> ExportLatest()
        {
            string OutputFolder = $@"{ _settings.BaseFilePath}\{_settings.OutputFilePath}";
            var codes = await _cacheService.GetPoliceCodes();
            var fromDb = await _repository.GetForOutput();
            var orgList = (from pTime in fromDb join cRef in codes on pTime.WcpId equals cRef.Infinium_Codes
                          select new FinalResult(pTime, cRef)).ToList();
            var otcList = orgList.Where(p => p.duplicate).Select(p => new FinalResult
            {
                EmployeeNumber = p.EmployeeNumber,
                AssignmentNumber = p.AssignmentNumber,
                Date = p.Date,
                Hours = p.Hours,
                HoursTypeIndicator = p.HoursTypeIndicator,
                PayrollTimeType = "STRAIGHT OT POLICE",
                Comments = p.Comments,
                OperationType = p.OperationType,
                duplicate = p.duplicate,
            });
         
            var combined = orgList.Concat(otcList).OrderBy( p => p.Date).ThenBy(p => p.EmployeeNumber).ToList();   // otc duplicated

            File.WriteAllBytes(Path.Combine(OutputFolder, "result.csv"), CsvExtensions.SaveToCSV(combined));
            return combined;
        }


    }
}
