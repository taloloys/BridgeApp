namespace DeviceBridge.Models
{
	public class StartEnrollmentRequest
	{
		public string finger_type { get; set; } = "index";
		public int quality_threshold { get; set; } = 60;
	}

	public class StartEnrollmentResponse
	{
		public bool success { get; set; }
		public string session_id { get; set; }
		public string status { get; set; }
		public string message { get; set; }
	}

	public class EnrollmentProgressResponse
	{
		public string session_id { get; set; }
		public int progress { get; set; }
		public string status { get; set; }
		public string instruction { get; set; }
		public string template { get; set; }
		public double? quality { get; set; }
		public string error { get; set; }
	}

	public class EnrollmentSession
	{
		public string SessionId { get; set; }
		public System.DateTime StartTime { get; set; }
		public string FingerType { get; set; }
		public int QualityThreshold { get; set; }
		public string Status { get; set; } = "waiting";
		public int Progress { get; set; } = 0;
		public string Instruction { get; set; } = "Place your finger on the scanner...";
		public string Template { get; set; }
		public double? Quality { get; set; }
		public string Error { get; set; }
		public FingerprintEnroll EnrollmentService { get; set; }
		public bool IsActive { get; set; } = true;
	}
}
