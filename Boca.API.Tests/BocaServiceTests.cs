using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Models.DTO;
using BocaAPI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Boca.API.Tests
{
    public sealed class BocaServiceTests : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(), "boca-tests-" + Guid.NewGuid().ToString("N"));
        private readonly FakeRepository _repo = new();
        private readonly FakeEmail _email = new();
        private readonly BocaService _service;

        public BocaServiceTests()
        {
            foreach (var d in new[] { "input", "results", "archive" })
                Directory.CreateDirectory(Path.Combine(_base, d));
            var settings = Options.Create(new Settings
            {
                BaseFilePath = _base,
                InputFilePath = "input",
                OutputFilePath = "results",
                ArchiveFilePath = "archive"
            });
            _service = new BocaService(_repo, NullLogger<BocaService>.Instance, settings, _email);
        }

        public void Dispose() => Directory.Delete(_base, true);

        private string LastInsertIdFile => Path.Combine(_base, "last-insert-id.txt");

        private static RawExportData Raw(string wcp, string shftab, DateTime str) => new()
        {
            PayId = 12288,
            WcpId = wcp,
            ROSDate = new DateTime(2026, 8, 31),
            STRDate = str,
            PayDuration = 6m,
            Shftab = shftab
        };

        [Fact]
        public async Task ExportLatest_UsesStrDate_DuplicatesOvertime_AndWritesCsv()
        {
            _repo.Output["batch-1"] = new List<RawExportData>
            {
                Raw("REG", "ADMIN", new DateTime(2026, 9, 1, 7, 30, 0)),
                Raw("OTC", "OT", new DateTime(2026, 8, 27, 15, 30, 0))
            };

            var result = await _service.ExportLatest("batch-1", "Test");

            Assert.Equal(3, result.Count);
            Assert.DoesNotContain(result, r => r.Date == "08/31/2026"); //ROSDate must not leak into output
            Assert.Collection(result,
                r => { Assert.Equal("08/27/2026", r.Date); Assert.Equal("OVERTIME POLICE", r.PayrollTimeType); },
                r => { Assert.Equal("08/27/2026", r.Date); Assert.Equal("STRAIGHT OT POLICE", r.PayrollTimeType); },
                r => { Assert.Equal("09/01/2026", r.Date); Assert.Equal("REGULAR POLICE", r.PayrollTimeType); });

            var file = Assert.Single(Directory.GetFiles(Path.Combine(_base, "results"), "Test_*.csv"));
            var lines = File.ReadAllLines(file);
            Assert.Equal(4, lines.Length); //header + 3 rows
            Assert.Contains("08/27/2026", lines[1]);
        }

        [Fact]
        public async Task ExportLatest_DropsRowsWithUnknownCode()
        {
            _repo.Output["b"] = new List<RawExportData> { Raw("ZZZ", "ADMIN", new DateTime(2026, 9, 1)) };

            var result = await _service.ExportLatest("b");

            Assert.Empty(result);
        }

        [Fact]
        public async Task ExportLastUpload_WithoutMarkerFile_ReturnsNull()
        {
            Assert.Null(await _service.ExportLastUpload());
            Assert.Empty(_repo.OutputRequests);
        }

        [Fact]
        public async Task ExportLastUpload_ReExportsStoredInsertId()
        {
            File.WriteAllText(LastInsertIdFile, "  batch-7\r\n");
            _repo.Output["batch-7"] = new List<RawExportData> { Raw("REG", "ADMIN", new DateTime(2026, 9, 2)) };

            var result = await _service.ExportLastUpload("Again");

            Assert.Equal(new[] { "batch-7" }, _repo.OutputRequests);
            Assert.Equal("09/02/2026", Assert.Single(result).Date);
            Assert.Single(Directory.GetFiles(Path.Combine(_base, "results"), "Again_*.csv"));
        }

        [Fact]
        public async Task Upload_StoresInsertId_LogsBadStrdt_ExportsStrDate_AndArchivesInput()
        {
            var input = Path.Combine(_base, "input", "VCS_test.csv");
            File.WriteAllText(input,
                "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,File Date,PAYDURAT,Comment\n" +
                "12288,OTC,OT,,,8/31/2026,8/27/2026 15:30,8/27/2026 21:30,OT,FALSE,PPE 09/13,6,\n" +
                "12289,XYZ,OT,,,8/31/2026,8/28/2026 15:30,8/28/2026 21:30,OT,FALSE,PPE 09/13,6,\n");

            await _service.UploadInputFileToDatabase();

            //valid row uploaded and remembered for ExportFile
            var uploaded = Assert.Single(_repo.Uploaded);
            Assert.Equal(new DateTime(2026, 8, 27, 15, 30, 0), uploaded.STRDT);
            Assert.Equal(_repo.LastInsertId, File.ReadAllText(LastInsertIdFile));

            //invalid row logged with its STRDT as the error date
            var error = Assert.Single(_repo.Errors);
            Assert.Equal(12289, error.EmployeeNumber);
            Assert.Equal(new DateTime(2026, 8, 28, 15, 30, 0), error.Date);

            //export used STRDate
            var exported = File.ReadAllLines(Assert.Single(Directory.GetFiles(Path.Combine(_base, "results"))));
            Assert.All(exported.Skip(1), l => Assert.Contains("08/27/2026", l));

            Assert.False(File.Exists(input));
            Assert.Single(Directory.GetFiles(Path.Combine(_base, "archive")));
            Assert.Single(_email.Sent);
        }

        [Fact]
        public async Task Upload_GarbageStrdt_IsLoggedAndOtherRowsStillLoad()
        {
            var input = Path.Combine(_base, "input", "VCS_garbage.csv");
            File.WriteAllText(input,
                "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,File Date,PAYDURAT,Comment\n" +
                "12288,REG,REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,\n" +
                "12289,REG,REG HRS,,,9/1/2026,9/1/2206 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,\n" +
                "12290,REG,REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,\n");

            await _service.UploadInputFileToDatabase();

            Assert.Equal(new[] { 12288, 12290 }, _repo.Uploaded.Select(r => r.PAYID));
            var error = Assert.Single(_repo.Errors);
            Assert.Equal(12289, error.EmployeeNumber);
            Assert.Contains("STRDT", error.Message);
            Assert.False(File.Exists(input)); //file archived, not stuck in input
        }

        private const string Header = "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,File Date,PAYDURAT,Comment\n";
        private static string Row(int payId, string wcp = "REG") => $"{payId},{wcp},REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,\n";

        [Fact]
        public async Task Email_AllInserted_IsSuccessWithCounts()
        {
            File.WriteAllText(Path.Combine(_base, "input", "VCS_ok.csv"), Header + Row(1) + Row(2) + Row(3));

            await _service.UploadInputFileToDatabase();

            Assert.Equal("SUCCESS - VCS file VCS_ok.csv: 3 of 3 records inserted", Assert.Single(_email.Sent));
            var body = Assert.Single(_email.Bodies);
            Assert.Contains("Total records in file:          3\n", body);
            Assert.Contains("Successfully inserted:          3\n", body);
            Assert.DoesNotContain("NOT loaded", body);
            Assert.DoesNotContain("already loaded", body);
        }

        [Fact]
        public async Task Email_EachKindOfRejection_IsFailureAndCountsAddUp()
        {
            _repo.RejectPayIds.Add(4);
            _repo.AlreadyLoadedPayIds.Add(5);
            File.WriteAllText(Path.Combine(_base, "input", "VCS_mixed.csv"),
                Header + Row(1) + Row(2) +
                "abc,REG,REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,\n" + //unreadable PAYID
                Row(3, "XYZ") +                                                                //fails validation
                Row(4) +                                                                       //rejected by database
                Row(5));                                                                       //duplicate, skipped

            await _service.UploadInputFileToDatabase();

            Assert.Equal("FAILURE - VCS file VCS_mixed.csv: 3 of 6 records NOT loaded", Assert.Single(_email.Sent));
            var body = Assert.Single(_email.Bodies);
            Assert.Contains("Total records in file:          6\n", body);
            Assert.Contains("Successfully inserted:          2\n", body);
            Assert.Contains("Skipped, already loaded before: 1\n", body);
            Assert.Contains("NOT loaded:                     3\n", body);
            Assert.Contains("  Unreadable (bad format):      1\n", body);
            Assert.Contains("  Failed validation:            1\n", body);
            Assert.Contains("  Rejected by database:         1\n", body);
        }

        [Fact]
        public async Task RowNum_IsCsvDataRowNumber_ForEveryKindOfError()
        {
            File.WriteAllText(Path.Combine(_base, "input", "VCS_rows.csv"),
                Header +
                Row(1) +                                                                          //row 1
                "abc,REG,REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,\n" +  //row 2 unreadable
                Row(3, "XYZ") +                                                                   //row 3 fails validation
                Row(4) +                                                                          //row 4
                Row(5));                                                                          //row 5

            await _service.UploadInputFileToDatabase();

            Assert.Equal(new[] { 2, 3 }, _repo.Errors.Select(e => e.RowNum).OrderBy(n => n));
            Assert.Equal(3, _repo.Errors.Single(e => e.EmployeeNumber == 3).RowNum);
            //records sent to the database carry their csv row, which BocaRepository logs if the insert is rejected
            Assert.Equal(new[] { 1, 4, 5 }, _repo.Uploaded.Select(r => r.RowNum));
        }

        [Fact]
        public async Task CommentLineBreaks_AreNotRecords_AndDoNotMakeItAFailure()
        {
            File.WriteAllText(Path.Combine(_base, "input", "VCS_comments.csv"),
                Header +
                "1,REG,REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,Detail\n" +
                "2026-008080- Knock and talk,,,,,,,,,,PPE 09/13,,\n" +   //rest of the comment above
                ",,,,,,,,,,PPE 09/13,,\n" +                              //empty line inside a comment
                "26-7906,,,,,,,,,,PPE 09/13,,\n" +
                Row(2));

            await _service.UploadInputFileToDatabase();

            Assert.Equal("SUCCESS - VCS file VCS_comments.csv: 2 of 2 records inserted", Assert.Single(_email.Sent));
            var body = Assert.Single(_email.Bodies);
            Assert.Contains("Total records in file:          2\n", body);
            Assert.Contains("Lines ignored, not records:     3 (line breaks inside VCS comments)\n", body);
            Assert.Equal(new[] { 2, 3, 4 }, _repo.Errors.Select(e => e.RowNum)); //still logged, as before
            Assert.Equal(new[] { 1, 5 }, _repo.Uploaded.Select(r => r.RowNum));
        }

        [Fact]
        public async Task UnreadableRowWithRecordData_IsStillAFailure()
        {
            //has a pay code and dates, so it is a real record that could not be read, not a comment fragment
            File.WriteAllText(Path.Combine(_base, "input", "VCS_bad.csv"),
                Header + Row(1) + ",REG,REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,PPE,8,\n");

            await _service.UploadInputFileToDatabase();

            Assert.Equal("FAILURE - VCS file VCS_bad.csv: 1 of 2 records NOT loaded", Assert.Single(_email.Sent));
            Assert.Contains("  Unreadable (bad format):      1\n", Assert.Single(_email.Bodies));
        }

        [Fact]
        public async Task Email_OnlyDuplicates_IsStillSuccess()
        {
            _repo.AlreadyLoadedPayIds.Add(1);
            File.WriteAllText(Path.Combine(_base, "input", "VCS_again.csv"), Header + Row(1));

            await _service.UploadInputFileToDatabase();

            Assert.Equal("SUCCESS - VCS file VCS_again.csv: 0 of 1 records inserted", Assert.Single(_email.Sent));
            Assert.Contains("Skipped, already loaded before: 1\n", Assert.Single(_email.Bodies));
        }

        [Fact]
        public async Task Upload_WithNothingLoaded_DoesNotWriteMarkerFile()
        {
            File.WriteAllText(Path.Combine(_base, "input", "bad.csv"),
                "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,File Date,PAYDURAT,Comment\n" +
                "12289,XYZ,OT,,,8/31/2026,8/28/2026 15:30,8/28/2026 21:30,OT,FALSE,PPE,6,\n");

            await _service.UploadInputFileToDatabase();

            Assert.False(File.Exists(LastInsertIdFile));
            Assert.Empty(Directory.GetFiles(Path.Combine(_base, "results")));
        }

        private sealed class FakeRepository : IBocaRepository
        {
            public Dictionary<string, List<RawExportData>> Output { get; } = new();
            public List<string> OutputRequests { get; } = new();
            public List<VCSExport> Uploaded { get; } = new();
            public List<Error> Errors { get; } = new();
            public HashSet<int> RejectPayIds { get; } = new();        //simulate the database rejecting these records
            public HashSet<int> AlreadyLoadedPayIds { get; } = new(); //simulate MERGE skipping these as duplicates
            public string LastInsertId { get; private set; }

            public Task<List<PoliceCode>> GetPoliceCodes() => Task.FromResult(new List<PoliceCode>
            {
                new() { Infinium_Codes = "REG", Oracle = "REGULAR POLICE", HourType = "Regular" },
                new() { Infinium_Codes = "OTC", Oracle = "OT CASH POLICE", HourType = "Overtime" },
                new() { Infinium_Codes = "CTE", Oracle = "COMP TIME EARNED", HourType = "Overtime" }
            });

            //mimic the SQL: store rows, return them the way police_master would
            public Task<(IEnumerable<RawExportData> Inserted, int Failed)> UploadToDatabase(List<VCSExport> records, string FileName, string InsertId)
            {
                Uploaded.AddRange(records);
                LastInsertId = InsertId;
                var toInsert = records.Where(r => !RejectPayIds.Contains(r.PAYID) && !AlreadyLoadedPayIds.Contains(r.PAYID)).ToList();
                Output[InsertId] = toInsert.Select(r => new RawExportData
                {
                    PayId = r.PAYID, WcpId = r.WCPID, ROSDate = r.ROSDT, STRDate = r.STRDT,
                    PayDuration = r.PAYDURAT, Comment = r.Comment, Shftab = r.SHFTAB
                }).ToList();
                return Task.FromResult<(IEnumerable<RawExportData>, int)>((Output[InsertId], records.Count(r => RejectPayIds.Contains(r.PAYID))));
            }

            public Task<IEnumerable<RawExportData>> GetForOutput(string InsertId)
            {
                OutputRequests.Add(InsertId);
                return Task.FromResult<IEnumerable<RawExportData>>(Output.TryGetValue(InsertId, out var rows) ? rows : new List<RawExportData>());
            }

            public void LogError(Error er) => Errors.Add(er);
            public Task<List<Error>> GetErrors() => Task.FromResult(Errors);
            public Task DeleteErrors() => Task.CompletedTask;
            public Task Archive() => Task.CompletedTask;
        }

        private sealed class FakeEmail : IEmail
        {
            public List<string> Sent { get; } = new();
            public List<string> Bodies { get; } = new();
            public Task Send(string content, string subject) { Sent.Add(subject); Bodies.Add(content); return Task.CompletedTask; }
        }
    }
}
