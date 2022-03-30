using CsvHelper.Configuration.Attributes;


namespace BocaAPI.Models.DTO
{
    public class VCSExport
    {
        [Name("PAYID")] public int PAYID { get; set; }
        [Name("WCPID")] public string WCPID { get; set; }
        [Name("WCABR")] public string WCABR { get; set; }
        [Name("ReasonCode")] public string ReasonCode { get; set; }
        [Name("Reason")] public string Reason { get; set; }
        [Name("ROSDT")] public DateTime ROSDT { get; set; }
        [Name("STRDT")] public DateTime STRDT { get; set; }
        [Name("ENDDT")] public DateTime ENDDT { get; set; }
        [Name("SHFTAB")] public string SHFTAB { get; set; }
        [Name("REMOVED")] public string REMOVED { get; set; }
        [Name("RECTYP")] public string RECTYP { get; set; }
        [Name("PAYDURAT")] public decimal PAYDURAT { get; set; }
        [Name("Comment")] public string Comment { get; set; }

    }
}
