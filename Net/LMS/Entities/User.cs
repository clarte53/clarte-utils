using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class User
	{
		public long Id { get; set; }
		public string Username { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public Group Group { get; set; }
		public Organization Organization { get; set; }
		public bool IsTrainer { get; set; }
		public string Token { get; set; }
	}
}
