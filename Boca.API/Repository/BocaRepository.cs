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
        //public async Task<List<RawExportData>> GetForOutput() => (await db.QueryAsync<PoliceMasterExport>(
        //    @"SELECT id, PayId, WcpId, ROSDate, STRDate, ENDDate, Removed, PayDuration, 'STREIGHT OF POLICE' as PayrollTimeType, Comment FROM dbo.police_master
        //    union all
        //    SELECT id, PayId, WcpId, ROSDate, STRDate, ENDDate, Removed, PayDuration, 'OVERTIME POLICE' as PayrollTimeType, Comment FROM dbo.police_master
        //    where WcpId = 'OT' OR WcpId= 'OTC'
        //   ")).ToList();

        public async Task<IEnumerable<RawExportData>> UploadToDatabase(List<VCSExport> records)
        {
            // truncate receiving table
            await db.ExecuteAsync("truncate table temp_police_master; ") ;
             
            //insert into temp
            await db.ExecuteAsync(
                @"INSERT into temp_police_master
                  ([payid],[wcpid],[Wcabr],[reasoncode],[reason],[rosdate],[strdate],[enddate],[shftab],[removed],[rectype],[payduration],[comment])  
                  VALUES(@PAYID,
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
                           @Comment)", records);

            //this section should go first inside next queryAsync call if we use temporary table method
            //For now we are using a real table and clearing it when we run export
            //
            //await db.ExecuteAsync(
            // @"
            //        CREATE TABLE #NewlyInserted(
	           //         PayId  int,
	           //         WcpId [nvarchar](8),
	           //         ROSDate [smalldatetime],
	           //         PayDuration [numeric](18, 3),
	           //         [Comment] [nvarchar](1028)
            //         )"
            // );

            var rtn = await db.QueryAsync<RawExportData>(
            @"
--  we are using real table in this version
--            CREATE TABLE #NewlyInserted(
--	                PayId  int,
--                    WcpId[nvarchar](8),
--                    ROSDate[smalldatetime],
--                    PayDuration[numeric](18, 3),
--                    [Comment][nvarchar](1028)
--                    );
            MERGE INTO police_master t 
                USING temp_police_master s
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
                  select * from NewlyInserted;   --If we decide to use temp table we need to  change NewlyInserted to #NewlyInserted everywhere
                "
                );
            return rtn;

        }

        public async Task<IEnumerable<RawExportData>> GetForOutput()
        {
            var rtn = await db.QueryAsync<RawExportData>(
                @"SELECT * FROM NewlyInserted;"
               );
            await db.ExecuteAsync("truncate table NewlyInserted; ");
            return rtn;
        }

    }
}
