using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ApplicationSummary
	{
		public User User { get; set; }
		public Application Application { get; set; }
		public TimeSpan ExerciseDuration { get; set; }
		public TimeSpan SpectatorDuration { get; set; }
		public TimeSpan DebriefDuration { get; set; }
	}
}
