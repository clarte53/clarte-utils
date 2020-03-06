using System;
using System.Net;
using UnityEngine.Events;

namespace CLARTE.Net.Discovery
{
	public static class Events
	{
		[Serializable]
		public class OnDiscoveredCallback : UnityEvent<IPAddress, ushort, IServiceInfo>
		{

		}

		[Serializable]
		public class OnLostCallback : UnityEvent<IPAddress, ushort, IServiceInfo>
		{

		}
	}
}
