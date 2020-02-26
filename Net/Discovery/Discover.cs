using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using CLARTE.Serialization;

namespace CLARTE.Net.Discovery
{
	[RequireComponent(typeof(Broadcaster))]
	public abstract class Discover<T> : MonoBehaviour where T : Enum
	{
		public class Remote
		{
			#region Members
			protected IPEndPoint endPoint;
			protected T type;
			#endregion

			#region Constructors
			public Remote(T type, IPAddress ip, ushort port)
			{
				this.type = type;

				endPoint = new IPEndPoint(ip, port);
			}
			#endregion

			#region Getters / Setters
			public IPAddress IPAddress
			{
				get
				{
					return new IPAddress(endPoint.Address.GetAddressBytes(), endPoint.Address.ScopeId);
				}
			}

			public ushort Port
			{
				get
				{
					return (ushort) endPoint.Port;
				}
			}

			public T Type
			{
				get
				{
					return type;
				}
			}
			#endregion

			#region Overloads for use as HashSet keys
			public override bool Equals(object comparand)
			{
				if(!(comparand is Remote))
				{
					return false;
				}
				if(((Remote) comparand).endPoint.Equals(endPoint))
				{
					return ((Remote) comparand).type.Equals(type);
				}

				return false;
			}

			public override int GetHashCode()
			{
				return endPoint.GetHashCode() ^ type.GetHashCode();
			}
			#endregion
		}

		#region Members
		protected const ushort beaconBufferSize = 5;

		public T type;
		public Negotiation.Server server;

		protected Broadcaster broadcast;
		protected HashSet<Remote> discovered;
		#endregion

		#region Abstract methods
		protected abstract void OnDiscovered(T type, IPAddress ip, ushort port);
		protected abstract void OnLost(T type, IPAddress ip, ushort port);
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
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
			if(datagram.Length == beaconBufferSize && ComputeControlSum(datagram) == datagram[datagram.Length - 1])
			{
				bool connected = datagram[0] == 1;
				T remote_type = (T) Enum.ToObject(typeof(T), datagram[1]);
				ushort remote_port = new Converter16(datagram[2], datagram[3]).UShort;

				Remote remote = new Remote(remote_type, ip, remote_port);

				bool already_discovered = discovered.Contains(remote);

				if(connected && !already_discovered)
				{
					discovered.Add(remote);

					OnDiscovered(remote_type, ip, remote_port);
				}
				else if(!connected && already_discovered)
				{
					discovered.Remove(remote);

					OnLost(remote_type, ip, remote_port);
				}
			}
		}

		protected void SendBeacon(bool connected)
		{
			if(server != null)
			{
				if((ulong) ((object) type) > 255)
				{
					throw new ArgumentException("The type enum values must be in range [0, 255]", "type");
				}

				Converter16 port_bytes = new Converter16(server.port);

				byte[] buffer = new byte[beaconBufferSize];

				buffer[0] = (byte) (connected ? 0x1 : 0x0);
				buffer[1] = (byte) ((object) type);
				buffer[2] = port_bytes.Byte1;
				buffer[3] = port_bytes.Byte2;
				buffer[4] = ComputeControlSum(buffer);

				broadcast.Send(buffer, beaconBufferSize);
			}
		}

		protected byte ComputeControlSum(byte[] data)
		{
			byte result = 0;

			for(int i = 0; i < data.Length - 1; i++)
			{
				result ^= data[i];
			}

			return result;
		}
		#endregion
	}
}
