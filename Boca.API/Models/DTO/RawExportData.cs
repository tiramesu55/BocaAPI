namespace BocaAPI.Models.DTO
{
    public class RawExportData
    {
        public int PayId { get; set; }
        public string WcpId { get; set; }
        public DateTime ROSDate { get; set; }
        public decimal PayDuration { get; set; }
        public string Comment { get; set; }
        public string Shftab { get; set; }

    }
}
