using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using UnityEngine;
using CLARTE.Serialization;
using System.Collections;

namespace CLARTE.Net.Discovery
{
	[RequireComponent(typeof(Broadcaster))]
	public class Discover : MonoBehaviour, IEnumerable<Remote>
	{
		[Serializable]
		public class Service
		{
			#region Members
			public string identifier;
			public Negotiation.Server server;
			#endregion
		}

		protected class Datagram : IBinarySerializable
		{
			#region Members
			public bool valid;
			public bool connected;
			public ushort port;
			public string identifier;
			#endregion

			#region Constructors
			public Datagram() // Required for Binary deserialization
			{
				valid = false;
				connected = false;
				port = 0;
				identifier = null;
			}

			public Datagram(bool connected, ushort port, string identifier)
			{
				this.connected = connected;
				this.port = port;
				this.identifier = identifier;

				valid = true;
			}
			#endregion

			#region IBinarySerializable implementation
			public uint FromBytes(Binary serializer, Binary.Buffer buffer, uint start)
			{
				uint read = 0;

				read += serializer.FromBytes(buffer, start + read, out connected);
				read += serializer.FromBytes(buffer, start + read, out port);
				read += serializer.FromBytes(buffer, start + read, out identifier);

				byte computed_checksum = ComputeControlSum(buffer.Data, start, read);

				byte received_checksum;

				read += serializer.FromBytes(buffer, start + read, out received_checksum);

				valid = (received_checksum == computed_checksum);

				return read;
			}

			public uint ToBytes(Binary serializer, ref Binary.Buffer buffer, uint start)
			{
				uint written = 0;

				written += serializer.ToBytes(ref buffer, start + written, connected);
				written += serializer.ToBytes(ref buffer, start + written, port);
				written += serializer.ToBytes(ref buffer, start + written, identifier);
				written += serializer.ToBytes(ref buffer, start + written, ComputeControlSum(buffer.Data, start, written));

				return written;
			}
			#endregion

			#region Internal methods
			protected static byte ComputeControlSum(byte[] data, uint start, uint size)
			{
				byte result = 0;

				uint end = start + size;

				for(uint i = start; i < end; i++)
				{
					result ^= data[i];
				}

				return result;
			}
			#endregion
		}

		#region Members
		public Events.OnDiscoveredCallback onDiscovered;
		public Events.OnLostCallback onLost;
		public List<Service> advertise;
		[Range(0.1f, 300f)]
		public float heartbeat = 2f; // In seconds
		[Range(1, 100)]
		public ushort lostAfterMissedHeartbeat = 15;

		protected Binary serializer;
		protected Threads.Thread sender;
		protected Threads.Thread cleaner;
		protected ManualResetEvent stopSender;
		protected ManualResetEvent stopCleaner;
		protected Broadcaster broadcast;
		protected Dictionary<Remote, long> discovered;
		protected List<Remote> remotesEnumerator;
		protected List<Remote> pendingLost;
		#endregion

		#region IEnumerable implementation
		public IEnumerator<Remote> GetEnumerator()
		{
			remotesEnumerator.Clear();

			lock(discovered)
			{
				remotesEnumerator.AddRange(discovered.Keys);
			}

			return remotesEnumerator.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
			serializer = new Binary();

			discovered = new Dictionary<Remote, long>();
			remotesEnumerator = new List<Remote>();
			pendingLost = new List<Remote>();

			broadcast = GetComponent<Broadcaster>();
		}

		protected void OnEnable()
		{
			if(sender == null)
			{
				stopSender = new ManualResetEvent(false);
				stopCleaner = new ManualResetEvent(false);

				broadcast.onReceive.AddListener(OnReceive);

				sender = new Threads.Thread(Sender);
				cleaner = new Threads.Thread(Cleaner);

				sender.Start();
				cleaner.Start();
			}
		}

		public void OnDisable()
		{
			stopSender?.Set();
			stopCleaner?.Set();

			if(sender != null)
			{
				SendBeacon(false);
			}

			if(broadcast != null)
			{
				broadcast.onReceive.RemoveListener(OnReceive);
			}

			sender?.Join();
			cleaner?.Join();

			stopSender?.Dispose();
			stopCleaner?.Dispose();

			discovered.Clear();

			sender = null;
			cleaner = null;
			stopSender = null;
			stopCleaner = null;
		}
		#endregion

		#region Internal methods
		protected void OnReceive(IPAddress ip, int port, byte[] datagram)
		{
			if(datagram != null && datagram.Length > 0)
			{
				Datagram deserialized = null;

				try
				{
					deserialized = serializer.Deserialize(datagram) as Datagram;
				}
				catch(Binary.DeserializationException) { }

				if(deserialized != null)
				{
					Remote remote = new Remote(deserialized.identifier, ip, deserialized.port);

					bool already_discovered;

					lock(discovered)
					{
						already_discovered = discovered.ContainsKey(remote);
					}

					if(deserialized.connected)
					{
						if(already_discovered)
						{
							lock(discovered)
							{
								discovered[remote] = GetCurrentTime();
							}
						}
						else
						{
							lock(discovered)
							{
								discovered.Add(remote, GetCurrentTime());
							}

							onDiscovered.Invoke(deserialized.identifier, ip, deserialized.port);
						}
					}
					else if(!deserialized.connected && already_discovered)
					{
						lock(discovered)
						{
							discovered.Remove(remote);
						}

						onLost.Invoke(deserialized.identifier, ip, deserialized.port);
					}
				}
			}
		}

		protected void SendBeacon(bool connected)
		{
			if(advertise != null)
			{
				foreach(Service service in advertise)
				{
					if(service.server != null && !string.IsNullOrEmpty(service.identifier))
					{
						byte[] data = null;

						try
						{
							data = serializer.Serialize(new Datagram(
								connected && service.server.CurrentState == Negotiation.Base.State.RUNNING,
								service.server.port,
								service.identifier
							));
						}
						catch(Binary.SerializationException) { }

						if(data != null && data.Length > 0)
						{
							broadcast.Send(data, data.Length);
						}
					}
				}
			}
		}

		protected void Sender()
		{
			while(!stopSender.WaitOne((int) (heartbeat * 1000)))
			{
				SendBeacon(true);
			}
		}

		protected void Cleaner()
		{
			while(!stopCleaner.WaitOne((int) (heartbeat * 1000)))
			{
				pendingLost.Clear();

				lock(discovered)
				{
					foreach(KeyValuePair<Remote, long> pair in discovered)
					{
						if(GetCurrentTime() - pair.Value >= (lostAfterMissedHeartbeat + 1) * 1000 * heartbeat)
						{
							pendingLost.Add(pair.Key);
						}
					}

					foreach(Remote lost in pendingLost)
					{
						discovered.Remove(lost);
					}
				}

				foreach(Remote lost in pendingLost)
				{
					Threads.APC.MonoBehaviourCall.Instance.Call(() => onLost.Invoke(lost.Type, lost.IPAddress, lost.Port));
				}
			}
		}

		protected long GetCurrentTime()
		{
			return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		}
		#endregion
	}
}
