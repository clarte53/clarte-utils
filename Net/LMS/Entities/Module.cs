using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Module
	{
		public long id;
		public Application application;
		public Guid guid;
		public string name;
	}
}
