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

        public Task UploadToDatabase(List<VCSExport> records)
        {
            var sql = "";
            return null;
        }
    }
}
