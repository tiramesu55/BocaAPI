using BocaAPI.Interfaces;
using BocaAPI.Models.DTO;
using Dapper;
using System.Data;
using System.Data.SqlClient;

namespace BocaAPI.Repository
{
    public class BocaRepository : IBocaRepository
    {
        private IDbConnection db;

        public BocaRepository(string connectionString)
        {
            db = new SqlConnection(connectionString);
        }


        public async Task<List<PoliceCode>> GetPoliceCodes() => (await db.QueryAsync<PoliceCode>("SELECT * FROM dbo.police_codes")).ToList();

        public async Task<List<Error>> GetErrors() => (await db.QueryAsync<Error>("SELECT Message,TimeStamp, Exception, RowNum FROM ErrorLogs")).ToList();

        public async Task DeleteErrors() => await db.ExecuteAsync("truncate table ErrorLogs; ");

        public async Task<IEnumerable<RawExportData>> UploadToDatabase(List<VCSExport> records)
        {
            await db.ExecuteAsync("truncate table NewlyInserted; ");

            foreach (var rec in records)
            {

                await db.ExecuteAsync(
                @"
                MERGE INTO police_master t 
                    USING ( VALUES 
                        ( @PAYID,
                          @WCPID,
                          @WCABR,
                          @ReasonCode,
                          @Reason,
                          @ROSDT,
                          @STRDT,
                          @ENDDT,
                          @SHFTAB,
                          @REMOVED,
                          @RECTYP,
                          @PAYDURAT,
                          @Comment ) ) s([PayId], [WcpId],[WCABR], [ReasonCode], [Reason], [ROSDate],
                          [STRDate], [ENDDate], [SHFTAB], [Removed], [RecType], [PayDuration],
                          [Comment])
                    ON t.payid = s.payid
                       AND s.[WcpId] = t.[wcpid]
                       AND t.[Wcabr] = s.[Wcabr]
                       AND t.[rosdate] = s.[ROSDate]
                       AND t.[strdate] = s.[STRDate]
                       AND t.[enddate] = s.[ENDDate]
                       AND t.[shftab] = s.[SHFTAB]
                    WHEN NOT MATCHED THEN 
                        INSERT ([payid],[wcpid],[Wcabr],[reasoncode],[reason],[rosdate],[strdate],[enddate],[shftab],[removed],[rectype],[payduration],[comment])
                        VALUES ( s.[payid],
                               s.[wcpid],
                               s.[Wcabr],
                               s.[reasoncode],
                               s.[reason],
                               s.[rosdate],
                               s.[strdate],
                               s.[enddate],
                               s.[shftab],
                               s.[removed],
                               s.[rectype],
                               s.[payduration],
                               s.[comment] ) 
                               OUTPUT inserted.payid, inserted.wcpid,inserted.rosdate, inserted.payduration, inserted.comment INTO NewlyInserted ;
                    ", rec
                        );
            }
            var rtn = await db.QueryAsync<RawExportData>(" select * from NewlyInserted"); // --If we decide to use temp table we need to change NewlyInserted to #NewlyInserted everywhere
            return rtn;

        }

        public async Task<IEnumerable<RawExportData>> GetForOutput()
        {
            var rtn = await db.QueryAsync<RawExportData>(
                @"SELECT * FROM NewlyInserted;"
               );
          //  await db.ExecuteAsync("truncate table NewlyInserted; ");
            return rtn;
        }

    }
}
