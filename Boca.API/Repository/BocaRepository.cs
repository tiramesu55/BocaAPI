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

        public void LogError(Error er)
        {
            try
            {
                var x = db.Execute(@"insert into ErrorLogs (Message, TimeStamp, Exception, RowNum) 
                values (@Message, @TimeStamp, @Exception, @RowNum )", er);
            }
            catch(Exception ex)
            {

            }
            
        } 
        public async Task DeleteErrors() => await db.ExecuteAsync("truncate table ErrorLogs; ");

        public async Task<IEnumerable<RawExportData>> UploadToDatabase(List<VCSExport> records, string fn)
        {
            await db.ExecuteAsync("truncate table NewlyInserted; ");

            foreach (var rec in records)
            {
                //build bag
                var param = new DynamicParameters(rec);
                //add file name to bag
                param.Add("fn", fn, DbType.String, ParameterDirection.Input, fn.Length);
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
                          @Comment,
                          @fn  ) ) s([PayId], [WcpId],[WCABR], [ReasonCode], [Reason], [ROSDate],
                          [STRDate], [ENDDate], [SHFTAB], [Removed], [RecType], [PayDuration],
                          [Comment], [FileName])
                    ON t.payid = s.payid
                       AND s.[WcpId] = t.[wcpid]
                       AND t.[Wcabr] = s.[Wcabr]
                       AND t.[rosdate] = s.[ROSDate]
                       AND t.[strdate] = s.[STRDate]
                       AND t.[enddate] = s.[ENDDate]
                       AND t.[shftab] = s.[SHFTAB]
       -- AND t.payid = 0
                    WHEN NOT MATCHED THEN 
                        INSERT ([payid],[wcpid],[Wcabr],[reasoncode],[reason],[rosdate],[strdate],[enddate],[shftab],[removed],[rectype],[payduration],[comment], [FileName])
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
                               s.[comment],
                               s.[FileName] ) 
                               OUTPUT inserted.payid, inserted.wcpid,inserted.rosdate, inserted.payduration, inserted.comment INTO NewlyInserted ;
                    ", param
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
