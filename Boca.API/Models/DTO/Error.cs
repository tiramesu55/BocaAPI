namespace BocaAPI.Models.DTO
{
    public class Error
    {
        public string Message { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Exception { get; set; }
        public int RowNum { get; set; }
        public int? EmployeeNumber { get; set; }
        public string? PayrollTimeType { get; set; }
        public DateTime? Date { get; set; }
        public decimal? Hours { get; set; }
    }
}
