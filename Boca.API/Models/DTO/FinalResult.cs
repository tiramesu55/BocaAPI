using System.Globalization;
using CsvHelper.Configuration.Attributes;


namespace BocaAPI.Models.DTO
{
    public class FinalResult
    {
		public int EmployeeNumber { get; set; }
		public string AssignmentNumber { get; set; }
		public string Date { get; set; }
		public decimal Hours { get; set; }
		public DateTime? StartTime { get; set; }
		public DateTime? StopTime { get; set; }
		public char HoursTypeIndicator { get; set; }
		public string PayrollTimeType{ get; set; }
		public string Comments{ get; set; }
		public string OperationType{ get; set; }
		
		[Ignore]
		public bool duplicate { get; set; }

		public FinalResult() 
		{ 
		}
		public FinalResult(RawExportData export, PoliceCode rf) 
        {
			EmployeeNumber = export.PayId;
			AssignmentNumber = $"E{export.PayId}";
			Date = export.ROSDate.ToString(@"MM/dd/yyyy", CultureInfo.InvariantCulture);
			Hours = Math.Round(export.PayDuration, 2);
			Comments = ""; //Boca requested empty comment in result //export.Comment;
			StartTime = default;
			StopTime = default;
			PayrollTimeType = rf.Oracle;
			HoursTypeIndicator = rf.HourType[0];  //first char	
			OperationType = "ADD";
			duplicate =  export.Shftab=="OT" || export.WcpId == "OT" || export.WcpId == "OTC" ? true : false;
        }		
	}
}
