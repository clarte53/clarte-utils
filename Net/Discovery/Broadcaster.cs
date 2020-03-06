using System;
using System.Collections.Generic;
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
		public bool broadcastToLocalhost = false;

		protected UdpClient udp;
		protected IPEndPoint broadcastAddress;
		protected HashSet<IPAddress> localAddresses;
		protected Threads.Thread thread;
		protected ManualResetEvent stop;
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
			broadcastAddress = new IPEndPoint(IPAddress.Broadcast, port);

			udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));

			localAddresses = new HashSet<IPAddress>();

			if(!broadcastToLocalhost)
			{
				IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

				foreach(IPAddress ip in host.AddressList)
				{
					if(ip.AddressFamily == AddressFamily.InterNetwork)
					{
						localAddresses.Add(ip);
					}
				}

				udp.Client.MulticastLoopback = false;
			}

			stop = new ManualResetEvent(false);

			thread = new Threads.Thread(Listener);

			thread.Start();
		}

		protected void OnDestroy()
		{
			stop.Set();

			thread.Join();
			
			udp.Dispose();
			stop.Dispose();

			udp = null;
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
			while(!stop.WaitOne(0))
			{
				System.Threading.Tasks.Task<UdpReceiveResult> t = udp.ReceiveAsync();

				while(!t.Wait(100) && !stop.WaitOne(0)) { }

				if(t.Wait(0))
				{
					byte[] datagram = t.Result.Buffer;
					IPEndPoint from = t.Result.RemoteEndPoint;

					if(datagram.Length > 0 && (broadcastToLocalhost || !localAddresses.Contains(from.Address)))
					{
						Threads.APC.MonoBehaviourCall.Instance.Call(() => onReceive.Invoke(from.Address, from.Port, datagram));
					}
				}
			}
		}
		#endregion
	}
}
