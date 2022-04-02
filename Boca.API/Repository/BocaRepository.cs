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
        public async Task<List<PoliceMasterExport>> GetForOutput() => (await db.QueryAsync<PoliceMasterExport>(
            @"SELECT id, PayId, WcpId, ROSDate, STRDate, ENDDate, Removed, PayDuration, 'STREIGHT OF POLICE' as PayrollTimeType, Comment FROM dbo.police_master
            union all
            SELECT id, PayId, WcpId, ROSDate, STRDate, ENDDate, Removed, PayDuration, 'OVERTIME POLICE' as PayrollTimeType, Comment FROM dbo.police_master
            where WcpId = 'OT' OR WcpId= 'OTC'
           ")).ToList();

        public async Task UploadToDatabase(List<VCSExport> records)
        {
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

            var x = await db.ExecuteAsync(
            @"MERGE INTO police_master t 
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
                           s.[comment] ) OUTPUT inserted.* ;"
                );
        }

    }
}
