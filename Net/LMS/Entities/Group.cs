using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Group
	{
		public long Id { get; set; }
		public string Key { get; set; }
		public string Name { get; set; }
	}
}
