namespace BocaAPI.Models.DTO
{
    public class Error
    {
        public string Message { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Exception { get; set; }
        public int RowNum { get; set; }
    }
}
