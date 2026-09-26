using System.Text;
using BocaAPI.Extensions;
using BocaAPI.Models.DTO;
using Xunit;

namespace Boca.API.Tests
{
    public class CsvReadTests
    {
        private static List<CsvExtensions.CsvReadResult<VCSExport>> Read(string csv) =>
            new MemoryStream(Encoding.UTF8.GetBytes(csv)).ReadFromCsv<VCSExport>();

        //production header (input/VCS_2026_09_27-9-23-2026.csv) with a row whose ROSDT and STRDT differ
        private const string NewHeader = "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,RECTYP,PAYDURAT,Comment";
        private const string OtcRow = "12288,OTC,OT,MTG2,Meeting (comment: type of meeting),8/31/2026,8/27/2026 15:30,8/27/2026 21:30,OT,FALSE,PPE 09/13,6,Explorers";

        [Fact]
        public void RectypHeader_ReadsRectyp_AndKeepsDistinctStrdt()
        {
            var rec = Assert.Single(Read($"{NewHeader}\n{OtcRow}\n")).Record;

            Assert.Equal("PPE 09/13", rec.RECTYP);
            Assert.Equal(new DateTime(2026, 8, 31), rec.ROSDT);
            Assert.Equal(new DateTime(2026, 8, 27, 15, 30, 0), rec.STRDT);
            Assert.Equal(6m, rec.PAYDURAT);
        }

        [Fact]
        public void ProductionRow_EmptyRectyp_ReadsAsEmptyString()
        {
            //row 2 of input/VCS_2026_09_27-9-23-2026.csv
            var csv = $"{NewHeader}\n" +
                      "12288,OTC,OT,MTG2,Meeting (comment: type of meeting),9/14/2026,9/10/2026 15:30,9/10/2026 21:30,OT,FALSE,,6,2026-09-10 15:30:00-' 2026-09-10 21:30:00 Explorers\n";

            var read = Assert.Single(Read(csv));

            Assert.True(read.IsValid);
            Assert.Equal("", read.Record.RECTYP);
            Assert.Equal(new DateTime(2026, 9, 10, 15, 30, 0), read.Record.STRDT);
        }

        [Fact]
        public void FileDateHeaderWithoutRectyp_NothingIsReadable()
        {
            var csv = NewHeader.Replace("RECTYP", "File Date") + "\n" + OtcRow + "\n";

            var reads = Read(csv);

            //RECTYP is a required column: the header check fails and so does every data row
            Assert.NotEmpty(reads);
            Assert.All(reads, r => { Assert.False(r.IsValid); Assert.Contains("RECTYP", r.Errors); });
        }

        [Fact]
        public void RectypLongerThan50_IsReadUnchanged_ForTheValidatorToReject()
        {
            var longValue = new string('A', 60);
            var row = OtcRow.Replace("PPE 09/13", longValue);

            var rec = Assert.Single(Read($"{NewHeader}\n{row}\n")).Record;

            Assert.Equal(longValue, rec.RECTYP);
        }

        [Fact]
        public void RowNumber_IsDataRowNumber_HeaderExcluded_ForGoodAndBadRows()
        {
            var bad = OtcRow.Replace("8/27/2026 15:30", "");

            var reads = Read($"{NewHeader}\n{OtcRow}\n{bad}\n{OtcRow}\n");

            Assert.Equal(new int?[] { 1, 2, 3 }, reads.Select(r => r.RowNumber));
            Assert.Equal(new[] { true, false, true }, reads.Select(r => r.IsValid));
        }

        [Fact]
        public void EmptyStrdt_ReadsAsInvalidRow()
        {
            var row = OtcRow.Replace("8/27/2026 15:30", "");

            var read = Assert.Single(Read($"{NewHeader}\n{row}\n"));

            Assert.False(read.IsValid);
            Assert.Equal(1, read.RowNumber);
        }
    }
}
