using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ModuleSummary
	{
		public User User { get; set; }
		public Module Module { get; set; }
		public DateTime FirstUseDate { get; set; }
		public DateTime CompletionDate { get; set; }
		public TimeSpan Duration { get; set; }
		public uint NbChallengesValidated { get; set; }
	}
}
