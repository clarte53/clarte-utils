using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Application
	{
		public long Id { get; set; }
		public Guid Guid { get; set; }
		public string Name { get; set; }
	}
}
