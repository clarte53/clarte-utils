﻿using System;
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

		protected Binary serializer;
		protected Threads.Thread thread;
		protected ManualResetEvent stop;
		protected Broadcaster broadcast;
		protected HashSet<Remote> discovered;
		#endregion

		#region IEnumerable implementation
		public IEnumerator<Remote> GetEnumerator()
		{
			return discovered.GetEnumerator();
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

			stop = new ManualResetEvent(false);

			discovered = new HashSet<Remote>();

			broadcast = GetComponent<Broadcaster>();

			broadcast.onReceive.AddListener(OnReceive);

			thread = new Threads.Thread(Sender);

			thread.Start();
		}

		protected void OnDestroy()
		{
			stop.Set();

			thread.Join();

			stop.Dispose();

			SendBeacon(false);

			thread = null;
			stop = null;
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

					bool already_discovered = discovered.Contains(remote);

					if(deserialized.connected && !already_discovered)
					{
						discovered.Add(remote);

						onDiscovered.Invoke(deserialized.identifier, ip, deserialized.port);
					}
					else if(!deserialized.connected && already_discovered)
					{
						discovered.Remove(remote);

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
			while(!stop.WaitOne((int) (heartbeat * 1000)))
			{
				SendBeacon(true);
			}
		}
		#endregion
	}
}
