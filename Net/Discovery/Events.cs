using System;
using System.Net;
using UnityEngine.Events;

namespace CLARTE.Net.Discovery
{
	public static class Events
	{
		[Serializable]
		public class OnDiscoveredCallback : UnityEvent<string, IPAddress, ushort>
		{

		}

		[Serializable]
		public class OnLostCallback : UnityEvent<string, IPAddress, ushort>
		{

		}
	}
}
