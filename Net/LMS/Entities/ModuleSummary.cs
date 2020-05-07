using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ModuleSummary
	{
		public long user;
		public long module;
		public long firstUseDate; // In ticks, i.e. the number of 100-nanosecond intervals that have elapsed since January 1, 0001 at 00:00:00.000 in the Gregorian calendar.
		public long completionDate; // In ticks, i.e. the number of 100-nanosecond intervals that have elapsed since January 1, 0001 at 00:00:00.000 in the Gregorian calendar.
		public uint duration; // In seconds
		public uint nbChallengesValidated;
		public ExerciseSummary[] exercises;
	}
}
