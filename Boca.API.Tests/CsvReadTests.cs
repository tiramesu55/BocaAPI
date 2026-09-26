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

        //header and row taken from input/VCS_input_2026_09_13.csv
        private const string NewHeader = "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,File Date,PAYDURAT,Comment";
        private const string OtcRow = "12288,OTC,OT,MTG2,Meeting (comment: type of meeting),8/31/2026,8/27/2026 15:30,8/27/2026 21:30,OT,FALSE,PPE 09/13,6,Explorers";

        [Fact]
        public void FileDateHeader_ReadsIntoRectyp_AndKeepsDistinctStrdt()
        {
            var rec = Assert.Single(Read($"{NewHeader}\n{OtcRow}\n")).Record;

            Assert.Equal("PPE 09/13", rec.RECTYP);
            Assert.Equal(new DateTime(2026, 8, 31), rec.ROSDT);
            Assert.Equal(new DateTime(2026, 8, 27, 15, 30, 0), rec.STRDT);
            Assert.Equal(6m, rec.PAYDURAT);
        }

        [Fact]
        public void LegacyRectypHeader_StillReads()
        {
            var csv = "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,RECTYP,PAYDURAT,Comment\n" +
                      "12288,CTE,yyy,,,7/8/2022,3/8/2022 8:00,3/8/2022 16:00,OTC,,X,7.75,\n";

            var read = Assert.Single(Read(csv));

            Assert.True(read.IsValid);
            Assert.Equal("X", read.Record.RECTYP);
            Assert.Equal(new DateTime(2022, 3, 8, 8, 0, 0), read.Record.STRDT);
        }

        [Fact]
        public void NoRectypColumn_DefaultsToSpace()
        {
            var csv = "PAYID,WCPID,WCABR,ReasonCode,Reason,ROSDT,STRDT,ENDDT,SHFTAB,REMOVED,PAYDURAT,Comment\n" +
                      "12288,REG,REG HRS,,,9/1/2026,9/1/2026 7:30,9/1/2026 15:30,ADMIN,FALSE,8,\n";

            var read = Assert.Single(Read(csv));

            Assert.True(read.IsValid);
            Assert.Equal(" ", read.Record.RECTYP);
        }

        [Fact]
        public void RectypLongerThan50_IsTruncatedTo50()
        {
            var longValue = new string('A', 60);
            var row = OtcRow.Replace("PPE 09/13", longValue);

            var rec = Assert.Single(Read($"{NewHeader}\n{row}\n")).Record;

            Assert.Equal(new string('A', 50), rec.RECTYP);
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
