using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ApplicationSummary
	{
		public User user;
		public Application application;
		public TimeSpan exerciseDuration;
		public TimeSpan spectatorDuration;
		public TimeSpan debriefDuration;
	}
}
