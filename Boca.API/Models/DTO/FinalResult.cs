namespace BocaAPI.Models.DTO
{
    public class FinalResult
    {
		public int EmployeeNumber { get; set; }
		public string AssignmentNumber { get; set; }
		public DateTime Date { get; set; }
		public decimal Hours { get; set; }
		public char HoursTypeIndicator { get; set; }
		public string PayrollTimeType{ get; set; }
		public string Comments{ get; set; }
		public string OperationType{ get; set; }
		public bool duplicate { get; set; }

		public FinalResult() 
		{ 
		}
		public FinalResult(RawExportData export, PoliceCode rf) 
        {
			EmployeeNumber = export.PayId;
			AssignmentNumber = $"E{export.PayId}";
			Date = export.ROSDate;
			Hours = export.PayDuration;	 
			Comments = export.Comment;
			PayrollTimeType = rf.Oracle;
			HoursTypeIndicator = rf.HourType[0];  //first char	
			OperationType = "ADD";
			duplicate = export.WcpId == "OT" || export.WcpId == "OTC" ? true : false;
        }		
	}
}
