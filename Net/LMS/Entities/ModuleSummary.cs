using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ModuleSummary
	{
		public User user;
		public Module module;
		public DateTime firstUseDate;
		public DateTime completionDate;
		public TimeSpan duration;
		public uint nbChallengesValidated;
	}
}
