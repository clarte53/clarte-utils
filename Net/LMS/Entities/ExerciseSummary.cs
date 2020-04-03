using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ExerciseSummary
	{
		public User user;
		public Exercise exercise;
		public uint nbCompletions;
		public float gradeMax;
	}
}
