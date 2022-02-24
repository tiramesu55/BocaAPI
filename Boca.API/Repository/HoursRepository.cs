using Boca.API.Entities;
using Dapper;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace BocaAPI.Repository
{
    public class HoursRepository : IHoursRepository
    {
        private IDbConnection db;
        public HoursRepository( string connString)
        {
            db = new SqlConnection( connString);
        }
        public IEnumerable<SourceTime> GetHoursAsync()
        {
            return db.Query<SourceTime>("SELECT * FROM Master_Hours").ToList();
        }
    }
}
