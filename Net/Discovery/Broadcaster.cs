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

		protected class UDP : IDisposable
		{
			#region Members
			protected UdpClient udp;
			protected HashSet<IPAddress> localAddresses;
			protected Threads.Thread thread;
			protected ManualResetEvent stop;
			protected ReceiveCallback onReceive;
			protected bool broadcastToLocalhost;
			#endregion

			#region Constructors
			public UDP(IPEndPoint endpoint, HashSet<IPAddress> local_addresses, ReceiveCallback receive_callback, bool broadcast_to_localhost)
			{
				localAddresses = local_addresses;
				onReceive = receive_callback;
				broadcastToLocalhost = broadcast_to_localhost;

				udp = new UdpClient(endpoint);

				if(!broadcastToLocalhost)
				{
					udp.Client.MulticastLoopback = false;
				}

				stop = new ManualResetEvent(false);

				thread = new Threads.Thread(Listener);

				thread.Start();
			}
			#endregion

			#region IDisposable implementation
			private bool isDisposed = false;

			protected virtual void Dispose(bool disposing)
			{
				if(!isDisposed)
				{
					if(disposing)
					{
						// TODO: clear managed state
					}

					// TODO: clear non managed states and replace finalizer below.
					// TODO: set large fields to null.

					stop.Set();

					thread.Join();

					udp.Dispose();

					stop.Dispose();

					thread = null;
					stop = null;

					isDisposed = true;
				}
			}

			~UDP()
			{
				Dispose(false);
			}

			public void Dispose()
			{
				Dispose(true);

				GC.SuppressFinalize(this);
			}
			#endregion

			#region Public methods
			public void Send(IPEndPoint endpoint, byte[] datagram, int size)
			{
				udp.SendAsync(datagram, size, endpoint);
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

		#region Members
		public ReceiveCallback onReceive;
		public ushort port = 65535;
		public bool broadcastToLocalhost = false;

		protected List<UDP> udpClients;
		protected IPEndPoint broadcastAddress;
		protected HashSet<IPAddress> localAddresses;
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
			broadcastAddress = new IPEndPoint(IPAddress.Broadcast, port);

			localAddresses = new HashSet<IPAddress>();

			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

			foreach(IPAddress ip in host.AddressList)
			{
				if(ip.AddressFamily == AddressFamily.InterNetwork)
				{
					localAddresses.Add(ip);
				}
			}

			udpClients = new List<UDP>(localAddresses.Count);

			foreach(IPAddress ip in localAddresses)
			{
				udpClients.Add(new UDP(new IPEndPoint(ip, port), localAddresses, onReceive, broadcastToLocalhost));
			}
		}

		protected void OnDestroy()
		{
			foreach(UDP udp in udpClients)
			{
				udp.Dispose();
			}

			udpClients.Clear();

			udpClients = null;
		}
		#endregion

		#region Public methods
		public void Send(byte[] datagram, int size)
		{
			if(size > 0 && size <= datagram.Length)
			{
				foreach(UDP udp in udpClients)
				{
					udp.Send(broadcastAddress, datagram, size);
				}
			}
		}
		#endregion
	}
}
