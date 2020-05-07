using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ApplicationSummary
	{
		public long user;
		public long application;
		public uint exerciseDuration; // In seconds
		public uint spectatorDuration; // In seconds
		public uint debriefDuration; // In seconds
		public ModuleSummary[] modules;
	}
}
