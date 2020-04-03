using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class ExerciseSummary
	{
		public User User { get; set; }
		public Exercise Exercise { get; set; }
		public uint NbCompletions { get; set; }
		public float GradeMax { get; set; }
	}
}
