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

        public async Task UploadToDatabase(List<VCSExport> records)
        {
            await db.QueryAsync<PoliceMaster>(@"MERGE INTO police_master t
                USING ( VALUES ( @PAYID,
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
                      @Comment ) ) s([PayId], [WcpId], [ReasonCode], [Reason], [ROSDate],
                      [STRDate], [ENDDate], [SHFTAB], [Removed], [RecType], [PayDuration],
                      [Comment])
                ON t.payid = s.payid
                   AND s.[WcpId] = t.[wcpid]
                   AND t.vcabr = s.vcabr
                   AND t.[rosdate] = s.[ROSDate]
                   AND t.[strdate] = s.[STRDate]
                   AND t.[enddate] = s.[ENDDate]
                   AND t.[shftab] = s.[SHFTAB]
                WHEN NOT MATCHED THEN
                  INSERT
                  VALUES ( s.[payid],
                           s.[wcpid],
                           s.[reasoncode],
                           s.[reason],
                           s.[rosdate],
                           s.[strdate],
                           s.[enddate],
                           s.[shftab],
                           s.[removed],
                           s.[rectype],
                           s.[payduration],
                           s.[comment] )", records);

        }

    }
}
