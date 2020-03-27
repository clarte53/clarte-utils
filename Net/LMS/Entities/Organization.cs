using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Organization
	{
		public long id;
		public string key;
		public string name;
		public DateTime licenseExpiration;
	}
}
