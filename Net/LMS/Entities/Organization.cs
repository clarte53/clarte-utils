﻿using System;

namespace CLARTE.Net.LMS.Entities
{
	[Serializable]
	public class Organization
	{
		public long Id { get; set; }
		public string Key { get; set; }
		public string Name { get; set; }
		public DateTime LicenseExpiration { get; set; }
	}
}
