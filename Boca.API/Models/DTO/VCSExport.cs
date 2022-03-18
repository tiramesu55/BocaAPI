namespace BocaAPI.Models.DTO
{
    public class VCSExport
    {
        public int PAYID { get; set; }
        public string WCPID { get; set; }
        public string WCABR { get; set; }
        public string ReasonCode { get; set; }
        public string Reason { get; set; }
        public DateTime ROSDT { get; set; }
        public DateTime STRDT { get; set; }
        public DateTime ENDDT { get; set; }
        public string SHFTAB { get; set; }
        public string REMOVED { get; set; }
        public string RECTYP { get; set; }
        public decimal PAYDURAT { get; set; }
        public string Comment { get; set; }

    }
}
