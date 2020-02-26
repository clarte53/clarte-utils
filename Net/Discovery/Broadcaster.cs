using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace CLARTE.Net.Discovery
{
	public class Broadcaster : MonoBehaviour
	{
		[Serializable]
		public class ReceiveCallback : UnityEvent<IPAddress, int, byte[]>
		{

		}

		#region Members
		public ReceiveCallback onReceive;
		public ushort port = 65535;

		protected UdpClient udp;
		protected IPEndPoint broadcastAddress;
		protected Threads.Thread thread;
		protected ManualResetEvent stop;
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
			broadcastAddress = new IPEndPoint(IPAddress.Broadcast, port);

			udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));

			udp.Client.MulticastLoopback = false;

			stop = new ManualResetEvent(false);

			thread = new Threads.Thread(Listener);

			thread.Start();
		}

		protected void OnDestroy()
		{
			stop.Set();

			thread.Join();

			stop.Dispose();

			thread = null;
			stop = null;
		}
		#endregion

		#region Public methods
		public void Send(byte[] datagram, int size)
		{
			if(size > 0 && size <= datagram.Length)
			{
				udp.SendAsync(datagram, size, broadcastAddress);
			}
		}
		#endregion

		#region Internal methods
		protected void Listener()
		{
			IPEndPoint from = new IPEndPoint(0, 0);

			while(!stop.WaitOne(0))
			{
				byte[] datagram = udp.Receive(ref from);

				if(datagram.Length > 0)
				{
					Threads.APC.MonoBehaviourCall.Instance.Call(() => onReceive.Invoke(from.Address, from.Port, datagram));
				}
			}
		}
		#endregion
	}
}
