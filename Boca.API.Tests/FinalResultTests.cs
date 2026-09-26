using BocaAPI.Models.DTO;
using Xunit;

namespace Boca.API.Tests
{
    public class FinalResultTests
    {
        private static RawExportData Raw(string wcpId = "REG", string shftab = "ADMIN", decimal hours = 8m) => new()
        {
            PayId = 12288,
            WcpId = wcpId,
            ROSDate = new DateTime(2026, 8, 31),
            STRDate = new DateTime(2026, 8, 27, 15, 30, 0),
            PayDuration = hours,
            Shftab = shftab
        };

        private static PoliceCode Code(string infinium, string oracle = "REGULAR POLICE", string hourType = "Regular") =>
            new() { Infinium_Codes = infinium, Oracle = oracle, HourType = hourType };

        [Fact]
        public void Date_UsesStrDate_NotRosDate()
        {
            var result = new FinalResult(Raw(), Code("REG"));

            Assert.Equal("08/27/2026", result.Date);
        }

        [Fact]
        public void Date_IsFormattedMMddyyyy_WithoutTimeOfDay()
        {
            var raw = Raw();
            raw.STRDate = new DateTime(2026, 1, 5, 23, 59, 0);

            var result = new FinalResult(raw, Code("REG"));

            Assert.Equal("01/05/2026", result.Date);
        }

        [Theory]
        [InlineData("CTE", 6, 9)]
        [InlineData("CTEJ", 7.75, 11.62)] //11.625 -> Math.Round uses banker's rounding (to even)
        [InlineData("REG", 7.755, 7.76)]
        public void Hours_AreTimeAndAHalfOnlyForCteCodes(string code, decimal input, decimal expected)
        {
            var result = new FinalResult(Raw(code, hours: input), Code(code));

            Assert.Equal(expected, result.Hours);
        }

        [Theory]
        [InlineData("OT")]
        [InlineData("OTC")]
        public void OvertimeShift_WithNonCteCode_IsOvertimePoliceAndDuplicated(string shftab)
        {
            var result = new FinalResult(Raw("REG", shftab), Code("REG"));

            Assert.Equal("OVERTIME POLICE", result.PayrollTimeType);
            Assert.True(result.duplicate);
        }

        [Fact]
        public void OvertimeShift_WithCteCode_KeepsOracleCodeAndIsNotDuplicated()
        {
            var result = new FinalResult(Raw("CTE", "OT"), Code("CTE", "COMP TIME EARNED"));

            Assert.Equal("COMP TIME EARNED", result.PayrollTimeType);
            Assert.False(result.duplicate);
        }

        [Fact]
        public void MapsFixedFields()
        {
            var result = new FinalResult(Raw(), Code("REG", hourType: "Regular"));

            Assert.Equal(12288, result.EmployeeNumber);
            Assert.Equal("E12288", result.AssignmentNumber);
            Assert.Equal('R', result.HoursTypeIndicator);
            Assert.Equal("ADD", result.OperationType);
            Assert.Equal("", result.Comments);
            Assert.False(result.duplicate);
        }
    }
}
