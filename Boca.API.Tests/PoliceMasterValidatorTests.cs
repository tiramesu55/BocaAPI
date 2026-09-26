using BocaAPI.Models.DTO;
using BocaAPI.Validators;
using Xunit;

namespace Boca.API.Tests
{
    public class PoliceMasterValidatorTests
    {
        private readonly PoliceMasterValidator _validator = new(new List<string> { "REG", "OTC", "CTE" });

        internal static VCSExport ValidRecord() => new()
        {
            PAYID = 12288,
            WCPID = "REG",
            WCABR = "REG HRS",
            ROSDT = new DateTime(2026, 8, 31),
            STRDT = new DateTime(2026, 8, 31, 7, 30, 0),
            ENDDT = new DateTime(2026, 8, 31, 15, 30, 0),
            SHFTAB = "ADMIN",
            REMOVED = "FALSE",
            RECTYP = "",
            PAYDURAT = 8m
        };

        [Fact]
        public void ValidRecord_Passes()
        {
            var result = _validator.Validate(ValidRecord());

            Assert.True(result.IsValid, string.Join(";", result.Errors));
        }

        [Fact]
        public void MissingStrdt_Fails_OnStrdt()
        {
            var rec = ValidRecord();
            rec.STRDT = default;

            var result = _validator.Validate(rec);

            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal(nameof(VCSExport.STRDT), error.PropertyName);
        }

        [Fact]
        public void MissingRosdt_StillFails_OnRosdt()
        {
            var rec = ValidRecord();
            rec.ROSDT = default;

            var result = _validator.Validate(rec);

            var error = Assert.Single(result.Errors);
            Assert.Equal(nameof(VCSExport.ROSDT), error.PropertyName);
        }

        public static IEnumerable<object[]> SmallDateTimeRange()
        {
            foreach (var field in new[] { nameof(VCSExport.ROSDT), nameof(VCSExport.STRDT), nameof(VCSExport.ENDDT) })
            {
                yield return new object[] { field, new DateTime(1899, 12, 31, 23, 59, 0), false };
                yield return new object[] { field, new DateTime(1900, 1, 1), true };
                yield return new object[] { field, new DateTime(2079, 6, 6, 23, 59, 0), true };
                yield return new object[] { field, new DateTime(2079, 6, 7), false };
                yield return new object[] { field, new DateTime(9999, 12, 31), false };
            }
        }

        [Theory]
        [MemberData(nameof(SmallDateTimeRange))]
        public void Dates_MustFitSqlSmallDateTime(string field, DateTime value, bool expectedValid)
        {
            var rec = ValidRecord();
            typeof(VCSExport).GetProperty(field).SetValue(rec, value);

            var result = _validator.Validate(rec);

            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
                Assert.Equal(field, Assert.Single(result.Errors).PropertyName);
        }

        [Fact]
        public void MissingEndt_Fails_OnEnddt()
        {
            var rec = ValidRecord();
            rec.ENDDT = default;

            Assert.Equal(nameof(VCSExport.ENDDT), Assert.Single(_validator.Validate(rec).Errors).PropertyName);
        }

        //production VCS files send RECTYP empty; the column is nvarchar(50)
        [Theory]
        [InlineData("")]
        [InlineData("PPE 09/27")]
        public void Rectyp_EmptyOrUpTo50_Passes(string rectyp)
        {
            var rec = ValidRecord();
            rec.RECTYP = rectyp;

            Assert.True(_validator.Validate(rec).IsValid);
        }

        [Fact]
        public void Rectyp_Null_Fails()
        {
            var rec = ValidRecord();
            rec.RECTYP = null;

            Assert.Equal(nameof(VCSExport.RECTYP), Assert.Single(_validator.Validate(rec).Errors).PropertyName);
        }

        [Fact]
        public void Rectyp_LongerThan50_Fails()
        {
            var rec = ValidRecord();
            rec.RECTYP = new string('A', 51);

            Assert.Equal(nameof(VCSExport.RECTYP), Assert.Single(_validator.Validate(rec).Errors).PropertyName);
        }

        [Fact]
        public void UnknownWcpid_Fails()
        {
            var rec = ValidRecord();
            rec.WCPID = "XYZ";

            var error = Assert.Single(_validator.Validate(rec).Errors);
            Assert.Equal(nameof(VCSExport.WCPID), error.PropertyName);
        }
    }
}
