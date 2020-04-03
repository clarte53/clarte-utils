using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Module
	{
		public long Id { get; set; }
		public Application Application { get; set; }
		public Guid Guid { get; set; }
		public string Name { get; set; }
	}
}
