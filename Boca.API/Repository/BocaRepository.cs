using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Models.DTO;
using Dapper;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace BocaAPI.Repository
{
    public class BocaRepository : IBocaRepository
    {
        private IDbConnection db;
        public BocaRepository( string connString)
        {
            db = new SqlConnection( connString);
        }
        //public IEnumerable<SourceTime> GetHoursAsync()
        //{
        //    return db.Query<SourceTime>("SELECT * FROM Master_Hours").ToList();
        //}


        public async Task<List<PoliceCode>> GetPoliceCodes() => (await db.QueryAsync<PoliceCode>("SELECT * FROM dbo.police_codes")).ToList();

        public async void UploadToDatabase(List<VCSExport> records)
        {
            var sql = @"merge into police_master t using
                (values(@PAYID, @WCPID, @WCABR,@ReasonCode, @Reason, @ROSDT, @STRDT, @ENDDT, @SHFTAB, @REMOVED, @RECTYP, @PAYDURAT, @Comment))
                s([PayId], [WcpId], [ReasonCode], [Reason], [ROSDate],[STRDate], [ENDDate], [SHFTAB], [Removed], [RecType], [PayDuration], [Comment]) 
                on t.PayId = s.PayId and s.[WcpId] = t.[WcpId] and t.VCABR=s.VCABR and t.[ROSDate] = s.[ROSDate] and t.[STRDate] = s.[STRDate] and t.[ENDDate] = s.[ENDDate] and t.[SHFTAB] = s.[SHFTAB]
                when not matched then
                insert values(s.[PayId], s.[WcpId], s.[ReasonCode], s.[Reason], s.[ROSDate], s.[STRDate], s.[ENDDate], s.[SHFTAB], s.[Removed], s.[RecType], s.[PayDuration], s.[Comment]);";
            using var tran = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(sql, records, tran);
                tran.Commit();
            }
            catch (Exception ex)
            {
                tran.Rollback();
                //todo log exception to DB log table
            }
            return;
        }
    }
}
