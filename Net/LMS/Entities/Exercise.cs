using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Exercise
	{
		public long Id { get; set; }
		public Module Module { get; set; }
		public Guid Guid { get; set; }
		public string Name { get; set; }
		public byte Level { get; set; }
	}
}
