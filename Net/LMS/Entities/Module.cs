using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Module
	{
		public long id;
		public Application application;
		public string guid; // Guid
		public string name;
	}
}
