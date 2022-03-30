namespace BocaAPI.Models.DTO
{
    public class PoliceMasterExport
    {
        public int id { get; set; }
        public int PayId { get; set; }
        public string WcpId { get; set; }
       // public string ReasonCode { get; set; }
       // public string Reason { get; set; }
        public DateTime ROSDate { get; set; }
        public DateTime STRDate { get; set; }
        public DateTime ENDDate { get; set; }
     //   public string SHFTAB { get; set; }
        public bool Removed { get; set; }
     //   public string RecType { get; set; }
        public decimal PayDuration { get; set; }
        public string Comment { get; set; }
        public string PayrollTimeType { get; set; }
    }
}
