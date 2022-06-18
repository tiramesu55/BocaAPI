using System.Globalization;
using System.Text.RegularExpressions;
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

        private readonly IBocaRepository _repository;
        private readonly Settings _settings;
        private readonly IEmail _email;

        public BocaService( IBocaRepository repository, ILogger<BocaService> logger, IOptions<Settings> options, IEmail email) : base(logger)
        {
            _email = email;
            _repository = repository;
            _settings = options.Value;
        }
        public IBocaRepository Repository{get { return _repository; }}

        public IEmail Email{get { return _email;} }

        public async Task UploadInputFileToDatabase()
        {
            try
            {
                //  List<RawExportData> result = new List<RawExportData>();
                string InputFolder = $@"{ _settings.BaseFilePath}\{_settings.InputFilePath}";
                string OutputFolder = $@"{ _settings.BaseFilePath}\{_settings.OutputFilePath}";
                string ArchiveFolder = $@"{ _settings.BaseFilePath}\{_settings.ArchiveFilePath}";
                var file = Directory.GetFiles(InputFolder, "*.csv").FirstOrDefault();

                //check if there is a file to work with
                if (file == null)
                    return;
                var fnForRecord = Path.GetFileNameWithoutExtension(file);
                var fileName = Regex.Replace($@"{Path.GetFileNameWithoutExtension(file)}-{DateTime.UtcNow}{Path.GetExtension(file)}", $"[{new string(Path.GetInvalidFileNameChars())}]", "-").Replace(' ', '_');

                var policeCodes = await _repository.GetPoliceCodes(); // _cacheService.GetPoliceCodes();

                var infiniumCodes = policeCodes.Select(p => p.Infinium_Codes).ToList();

                var validator = new PoliceMasterValidator(infiniumCodes);

                var readResults = File.OpenRead(file).ReadFromCsv<VCSExport>();

                //log those that cannot be cast to the VCSSxport class
                readResults.Where(readResult => !readResult.IsValid || readResult.Record is null).ToList()
                         .ForEach(readResult => _repository.LogError(
                             new Error { RowNum = readResult.RowNumber.Value, Message = readResult.Errors, TimeStamp = DateTime.Now }));

                //now run record that were converted through validator
                var validatedRecords = readResults.Where(p => p.IsValid).Select((record, i) =>
                {
                    var validationResult = validator.Validate(record.Record);
                    return new
                    {
                        Number = i,
                        Record = record.Record,
                        IsValid = validationResult.IsValid,
                        Errors = validationResult.Errors
                                             .Select(e => e.ErrorMessage)
                                             .StringJoin()
                    };
                }).ToList();

                //get invalid records and log them
                var invalidRecords = validatedRecords.Where(record => !record.IsValid).ToList();
                invalidRecords.ForEach(r => _repository.LogError(
                             new Error {
                                 RowNum = r.Number, 
                                 Message = r.Errors, 
                                 TimeStamp = DateTime.Now,
                                 EmployeeNumber = r.Record.PAYID,
                                 PayrollTimeType = r.Record.WCPID,
                                 Date = r.Record.ROSDT,
                                 Hours = r.Record.PAYDURAT
                             }));

                //load valid records
                var validRecords = validatedRecords.Where(r => r.IsValid).ToList();
                //create a cookie
                var InsertId = Guid.NewGuid().ToString();
                var rtn = await _repository.UploadToDatabase(validRecords.Select(r => r.Record).ToList(), fnForRecord, InsertId);
                if (rtn?.Count() > 0)
                    await ExportLatest(InsertId);
                var body = CreateBody(readResults.Count(), invalidRecords.Count(), validRecords.Count(), rtn?.Count());
                var header = CreateHeader(invalidRecords.Count() != 0, fnForRecord);
                await Email.Send(body, header);
                File.Move(file, $@"{ArchiveFolder}\{fileName}", true);  //move with overwrite

                return;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex.Message, ex);
            }
        }

        public async Task Archive()
        {
            // call archive method on repository
            await _repository.Archive();
        }

        //Format email body
        private string CreateBody(int totalRecords, int errorRecords, int validRecords, int? addedRecords)
        {
            var res = $"Total Records: {totalRecords} \nNumber of error records: {errorRecords}\nRecords attempted to add: {validRecords}\nAdded Records: {addedRecords}";
            return res;
        }

        private string CreateHeader( bool hasErrors, string fileName)
        {
            if(hasErrors)
            {
                return $"File {fileName} has been loaded with errors";
            }
            else
            {
                return $"File {fileName} has been loaded successfully";
            }
        }
        public async Task<List<FinalResult>> ExportLatest( string InsertId, string FileName = "VCSTime")
        {
            string OutputFolder = $@"{ _settings.BaseFilePath}\{_settings.OutputFilePath}";
            var filename = $"{FileName}_{DateTime.Now.ToString("MMddyyyy_HHmm")}.csv";
            var codes = await _repository.GetPoliceCodes();
            var fromDb = await _repository.GetForOutput( InsertId );
            var orgList = (from pTime in fromDb join cRef in codes on pTime.WcpId equals cRef.Infinium_Codes
                          select new FinalResult(pTime, cRef)).ToList();
            var otcList = orgList.Where(p => p.duplicate).Select(p => new FinalResult
            {
                EmployeeNumber = p.EmployeeNumber,
                AssignmentNumber = p.AssignmentNumber,
                Date = p.Date,
                Hours = p.Hours,
                StartTime = p.StartTime,
                StopTime = p.StopTime,
                HoursTypeIndicator = p.HoursTypeIndicator,
                PayrollTimeType = "STRAIGHT OT POLICE",
                Comments = p.Comments,
                OperationType = p.OperationType,
               // duplicate = p.duplicate,
            });
         
            var combined = orgList.Concat(otcList).OrderBy( p => p.Date).ThenBy(p => p.EmployeeNumber).ToList();   // otc duplicated

            File.WriteAllBytes(Path.Combine(OutputFolder, filename), CsvExtensions.SaveToCSV(combined));
            return combined;
        }


    }
}
