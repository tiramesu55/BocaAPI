using System.Data.SqlClient;
using BocaAPI.Models.DTO;
using BocaAPI.Repository;
using Xunit;

namespace Boca.API.Tests
{
    //creates a throwaway database on (localdb)\MSSQLLocalDB with the police_master / ErrorLogs columns the repository uses
    public sealed class LocalDbFixture : IDisposable
    {
        private const string Master = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database=master";
        private readonly string _name = "BocaTests_" + Guid.NewGuid().ToString("N");
        public string ConnectionString => $@"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database={_name}";

        public LocalDbFixture()
        {
            Exec(Master, $"CREATE DATABASE [{_name}]");
            Exec(ConnectionString, @"
                CREATE TABLE dbo.police_master(
                    [id] int IDENTITY(1,1) PRIMARY KEY,
                    [PayId] int NOT NULL, [WcpId] nvarchar(8) NOT NULL, [WCABR] nvarchar(16) NULL,
                    [ReasonCode] nvarchar(16) NULL, [Reason] nvarchar(128) NULL,
                    [ROSDate] smalldatetime NOT NULL, [STRDate] smalldatetime NOT NULL, [ENDDate] smalldatetime NOT NULL,
                    [SHFTAB] nvarchar(16) NOT NULL, [Removed] bit NOT NULL, [RecType] nvarchar(50) NULL,
                    [PayDuration] numeric(18,3) NOT NULL, [Comment] nvarchar(1028) NULL,
                    [FileName] nvarchar(128) NOT NULL, [InsertId] nvarchar(50) NOT NULL);
                CREATE TABLE dbo.ErrorLogs(
                    [Id] int IDENTITY(1,1) PRIMARY KEY, [Message] nvarchar(max) NULL, [TimeStamp] datetime NULL,
                    [Exception] nvarchar(max) NULL, [RowNum] int NULL, [EmployeeNumber] int NULL,
                    [PayrollTimeType] nvarchar(50) NULL, [Date] datetime NULL, [Hours] decimal(18,3) NULL);");
        }

        public void Exec(string cs, string sql)
        {
            using var c = new SqlConnection(cs);
            c.Open();
            using var cmd = new SqlCommand(sql, c);
            cmd.ExecuteNonQuery();
        }

        public T Scalar<T>(string sql)
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
            using var cmd = new SqlCommand(sql, c);
            return (T)cmd.ExecuteScalar();
        }

        public void Dispose()
        {
            SqlConnection.ClearAllPools();
            Exec(Master, $"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]");
        }
    }

    [Trait("Category", "Integration")]
    public class BocaRepositoryIntegrationTests : IClassFixture<LocalDbFixture>
    {
        private readonly LocalDbFixture _db;

        public BocaRepositoryIntegrationTests(LocalDbFixture db)
        {
            _db = db;
            _db.Exec(_db.ConnectionString, "DELETE police_master; DELETE ErrorLogs;");
        }

        private static VCSExport Rec(int payId, string wcabr = "REG HRS")
        {
            var r = PoliceMasterValidatorTests.ValidRecord();
            r.PAYID = payId;
            r.WCABR = wcabr;
            r.RowNum = payId + 10; //csv row differs from list position, as it does when earlier rows failed validation
            return r;
        }

        [Fact]
        public async Task BadRecord_IsLogged_AndRecordsAfterItStillInsert()
        {
            var repo = new BocaRepository(_db.ConnectionString);
            //WCABR is nvarchar(16) and not length-validated: the middle record fails with a SQL truncation error
            var records = new List<VCSExport> { Rec(1), Rec(2, new string('X', 40)), Rec(3) };

            var (rows, failed) = await repo.UploadToDatabase(records, "file", "batch-1");
            var inserted = rows.ToList();

            Assert.Equal(1, failed);
            Assert.Equal(new[] { 1, 3 }, inserted.Select(r => r.PayId).OrderBy(p => p));
            Assert.All(inserted, r => Assert.Equal(new DateTime(2026, 8, 31, 7, 30, 0), r.STRDate));
            Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM ErrorLogs"));
            Assert.Equal(2, _db.Scalar<int>("SELECT EmployeeNumber FROM ErrorLogs"));
            Assert.Equal(12, _db.Scalar<int>("SELECT RowNum FROM ErrorLogs")); //the record's csv row, not its position in the batch
            Assert.StartsWith("Insert failed:", _db.Scalar<string>("SELECT Message FROM ErrorLogs"));
        }

        [Fact]
        public async Task SameBatchTwice_DoesNotDuplicateRows()
        {
            var repo = new BocaRepository(_db.ConnectionString);

            await repo.UploadToDatabase(new List<VCSExport> { Rec(1) }, "file", "batch-1");
            var second = await repo.UploadToDatabase(new List<VCSExport> { Rec(1) }, "file", "batch-2");

            Assert.Empty(second.Inserted);
            Assert.Equal(0, second.Failed);
            Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM police_master"));
        }

        [Fact]
        public async Task ConnectionFailure_StillThrows_SoFileIsRetried()
        {
            var repo = new BocaRepository(@"Server=(localdb)\NoSuchInstance_Boca;Integrated Security=true;Connect Timeout=2");

            await Assert.ThrowsAsync<SqlException>(() => repo.UploadToDatabase(new List<VCSExport> { Rec(1), Rec(2) }, "file", "batch-1"));
        }
    }
}
