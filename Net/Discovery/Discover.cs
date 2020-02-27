using System;
using System.Collections.Generic;
using System.Net;
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

		protected Binary serializer;
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

			discovered = new HashSet<Remote>();

			broadcast = GetComponent<Broadcaster>();

			broadcast.onReceive.AddListener(OnReceive);
		}

		protected void OnDestroy()
		{
			SendBeacon(false);
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
							data = serializer.Serialize(new Datagram(connected, service.server.port, service.identifier));
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
		#endregion
	}
}
