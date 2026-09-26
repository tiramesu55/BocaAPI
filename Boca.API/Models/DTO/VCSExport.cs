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
        //RECTYP is no longer sent by VCS (replaced by "File Date"); optional, defaults to " " when neither column exists
        //not validated anymore, so cap at the police_master.RecType size nvarchar(50)
        private string _rectyp = " ";
        [Name("RECTYP", "File Date")][Optional] public string RECTYP { get => _rectyp; set => _rectyp = value?.Length > 50 ? value[..50] : value; }
        [Name("PAYDURAT")] public decimal PAYDURAT { get; set; }
        [Name("Comment")] public string Comment { get; set; }

    }
}
