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
            List<RawExportData> result = new List<RawExportData>();
            string InputFolder = $@"{ _settings.BaseFilePath}\{_settings.InputFilePath}";
            string OutputFolder = $@"{ _settings.BaseFilePath}\{_settings.OutputFilePath}";
            string ArchiveFolder = $@"{ _settings.BaseFilePath}\{_settings.ArchiveFilePath}";
            foreach (var file in Directory.GetFiles(InputFolder, "*.csv"))
            {
                var fileName = Path.GetFileName(file);

                var policeCodes = await _cacheService.GetPoliceCodes();

                var infiniumCodes = policeCodes.Select(p => p.Infinium_Codes).ToList();

                var validator = new PoliceMasterValidator(infiniumCodes);

                var records = CsvExtensions.ReadFromCsv<VCSExport>(File.OpenRead(file));

                var validatedRecords = records.Select((r, i) =>
                {
                    var validationResult = validator.Validate(r);
                    return new { Number = i, Record = r, IsValid = validationResult.IsValid, Errors = validationResult.Errors.Select(e => e.ErrorMessage).StringJoin() };
                }).ToList();

                var invalidRecords = validatedRecords.Where(r => !r.IsValid).ToList();
                validatedRecords.Where(r => !r.IsValid).ToList().ForEach(r => _logger.LogInfo(r.Number, r.Errors));

                var rtn = await _repository.UploadToDatabase(validatedRecords.Where(r => r.IsValid).Select(r => r.Record).ToList());
                result.AddRange(rtn.ToList());

                File.Move(file, $@"{ArchiveFolder}\{fileName}", true);  //move with overwrite
            }
            return result;
        }
        public async Task<List<FinalResult>> ExportLatest( string FileName = "VCSTime")
        {
            string OutputFolder = $@"{ _settings.BaseFilePath}\{_settings.OutputFilePath}";
            var filename = $"{FileName}_{DateTime.Now.ToString("MMddyyyy")}.csv";
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

            File.WriteAllBytes(Path.Combine(OutputFolder, filename), CsvExtensions.SaveToCSV(combined));
            return combined;
        }


    }
}
