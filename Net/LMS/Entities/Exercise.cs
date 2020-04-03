using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Exercise
	{
		public long id;
		public Module module;
		public Guid guid;
		public string name;
		public byte level;
	}
}
