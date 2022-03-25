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


		public static explicit operator FinalResult(PoliceMaster policeMaster) => new FinalResult
        {
			EmployeeNumber = policeMaster.PayId,
			AssignmentNumber = $"E{policeMaster.PayId}",
			Date = policeMaster.ROSDate,
			Hours = policeMaster.PayDuration,
			HoursTypeIndicator = policeMaster.WcpId switch
            {
				"OT" => 'R',
				"OTC" => 'R',
				"REG" => 'R',
				"STR" => 'R',
				"STRC" => 'R',
				_ => 'A'
			}, 
			Comments = policeMaster.Comment,
			OperationType = "ADD"
        };		
	}
}
