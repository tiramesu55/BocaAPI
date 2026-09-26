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
                var validatedRecords = readResults.Where(p => p.IsValid).Select(record =>
                {
                    var validationResult = validator.Validate(record.Record);
                    //carry the csv data row number so a database rejection can be logged against it too
                    record.Record.RowNum = record.RowNumber.Value;
                    return new
                    {
                        Number = record.RowNumber.Value,
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
                                 Date = r.Record.STRDT,
                                 Hours = r.Record.PAYDURAT
                             }));

                //load valid records
                var validRecords = validatedRecords.Where(r => r.IsValid).ToList();
                //create a cookie
                var InsertId = Guid.NewGuid().ToString();
                var (inserted, dbRejected) = await _repository.UploadToDatabase(validRecords.Select(r => r.Record).ToList(), fnForRecord, InsertId);
                var insertedCount = inserted.Count();
                if (insertedCount > 0)
                {
                    await ExportLatest(InsertId);
                    //remember this batch so api/hours/ExportFile can re-export it (police_master has no insert timestamp to find it by)
                    File.WriteAllText(LastInsertIdFile, InsertId);
                }
                var fragments = readResults.Count(IsCommentFragment);
                var counts = new LoadCounts(
                    Total: readResults.Count - fragments,
                    Unreadable: readResults.Count(r => !r.IsValid) - fragments,
                    Invalid: invalidRecords.Count,
                    DbRejected: dbRejected,
                    Inserted: insertedCount,
                    CommentFragments: fragments);
                await Email.Send(CreateBody(Path.GetFileName(file), counts), CreateHeader(Path.GetFileName(file), counts));
                File.Move(file, $@"{ArchiveFolder}\{fileName}", true);  //move with overwrite

                return;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex.Message, ex);
            }
        }

        private string LastInsertIdFile => $@"{ _settings.BaseFilePath}\last-insert-id.txt";

        //re-export the most recent upload; returns null if nothing has been uploaded yet
        public async Task<List<FinalResult>> ExportLastUpload(string FileName = "VCSTime")
        {
            if (!File.Exists(LastInsertIdFile))
                return null;
            var InsertId = File.ReadAllText(LastInsertIdFile).Trim();
            return await ExportLatest(InsertId, FileName);
        }

        public async Task Archive()
        {
            // call archive method on repository
            await _repository.Archive();
        }

        //every record in the file ends up in exactly one bucket: Total = Unreadable + Invalid + DbRejected + AlreadyLoaded + Inserted
        public record LoadCounts(int Total, int Unreadable, int Invalid, int DbRejected, int Inserted, int CommentFragments = 0)
        {
            public int Failed => Unreadable + Invalid + DbRejected;
            //valid records the MERGE skipped because the same shift is already in police_master (e.g. file dropped twice)
            public int AlreadyLoaded => Total - Failed - Inserted;
        }

        //VCS does not quote Comment, so a line break in a comment starts a new "row" holding just the rest of the comment
        //(and the RECTYP). It is not a time record: no pay code, dates or hours, so it is not counted as a record
        private static readonly string[] RecordColumns = { "WCPID", "ROSDT", "STRDT", "ENDDT", "PAYDURAT" };
        public static bool IsCommentFragment(CsvExtensions.CsvReadResult<VCSExport> r) =>
            !r.IsValid && r.Fields != null && RecordColumns.All(c => !r.Fields.TryGetValue(c, out var v) || string.IsNullOrWhiteSpace(v));

        //Format email subject: SUCCESS only when no record was rejected; skipped duplicates are not a failure
        public static string CreateHeader(string fileName, LoadCounts c) =>
            c.Failed == 0
                ? $"SUCCESS - VCS file {fileName}: {c.Inserted} of {c.Total} records inserted"
                : $"FAILURE - VCS file {fileName}: {c.Failed} of {c.Total} records NOT loaded";

        //Format email body
        public static string CreateBody(string fileName, LoadCounts c)
        {
            var body = $"File: {fileName}\n" +
                       $"Processed: {DateTime.Now:MM/dd/yyyy HH:mm}\n\n" +
                       $"Total records in file:          {c.Total}\n" +
                       $"Successfully inserted:          {c.Inserted}\n";
            if (c.AlreadyLoaded > 0)
                body += $"Skipped, already loaded before: {c.AlreadyLoaded}\n";
            if (c.CommentFragments > 0)
                body += $"\nLines ignored, not records:     {c.CommentFragments} (line breaks inside VCS comments)\n";
            if (c.Failed > 0)
                body += $"\nNOT loaded:                     {c.Failed}\n" +
                        $"  Unreadable (bad format):      {c.Unreadable}\n" +
                        $"  Failed validation:            {c.Invalid}\n" +
                        $"  Rejected by database:         {c.DbRejected}\n\n" +
                        "Details of each record that was not loaded are in the ErrorLogs table (GET api/hours/GetErrors).\n" +
                        "Correct them and drop the file into the input folder again; records already loaded are skipped.\n";
            return body;
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
