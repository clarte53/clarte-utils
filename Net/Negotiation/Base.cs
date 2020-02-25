#if !NETFX_CORE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using CLARTE.Serialization;

namespace CLARTE.Net.Negotiation
{
	public abstract class Base : MonoBehaviour
	{
		protected enum State
		{
			STARTED,
			INITIALIZING,
			RUNNING,
			CLOSING,
			DISPOSED
		}

		protected class DropException : Exception
		{
			public DropException(string message) : base(message)
			{

			}
		}

		[Serializable]
		public class Credentials
		{
			#region Members
			public string username;
			public string password;
			#endregion
		}

		[Serializable]
		public class PortRange
		{
			#region Members
			public const ushort maxPoolSize = 1024;
			// Avoid IANA system or well-known ports that requires admin privileges
			public const ushort minAvailablePort = 1024;
			public const ushort maxavailablePort = 65535;

			public ushort minPort = minAvailablePort;
			public ushort maxPort = maxavailablePort;
			#endregion
		}

		protected class UdpConnectionParams
		{
			public Message.Negotiation.Parameters param;
			public UdpClient udp;
			public IPAddress remote;
			public ushort localPort;
		}

		#region Members
		protected static readonly TimeSpan defaultHeartbeat = new TimeSpan(5 * TimeSpan.TicksPerSecond);

		public List<PortRange> openPorts;

		protected Binary serializer;
		protected Dictionary<Guid, Connection.Base[]> openedChannels;
		protected HashSet<Connection.Tcp> initializedConnections;
		protected HashSet<ushort> availablePorts;
		protected Connection.Tcp monitor;
		protected State state;
		#endregion

		#region Abstract methods
		protected abstract void Dispose(bool disposing);
		protected abstract void Reconnect(Connection.Base connection);
		protected abstract void OnMonitorReceive(IPAddress address, Guid guid, ushort channel, byte[] data);
		public abstract ushort NbChannels { get; }
		public abstract IEnumerable<Channel> Channels { get; }
		#endregion

		#region Clean-up helpers
		protected void CloseMonitor()
		{
			if(monitor != null)
			{
				if(monitor.initialization != null)
				{
					monitor.initialization.Wait();
				}

				monitor.Close();

				monitor = null;
			}
		}

		protected void CloseInitializedConnections()
		{
			lock(initializedConnections)
			{
				foreach(Connection.Tcp connection in initializedConnections)
				{
					if(connection != null)
					{
						if(connection.initialization != null)
						{
							connection.initialization.Wait();
						}

						connection.Close();
					}
				}

				initializedConnections.Clear();
			}
		}

		protected void CloseOpenedChannels()
		{
			lock(openedChannels)
			{
				foreach(KeyValuePair<Guid, Connection.Base[]> pair in openedChannels)
				{
					foreach(Connection.Base connection in pair.Value)
					{
						if(connection != null)
						{
							connection.Close();
						}
					}
				}

				openedChannels.Clear();
			}
		}

		protected void Close(Connection.Base connection)
		{
			if(connection != null)
			{
				if(connection is Connection.Tcp)
				{
					lock(initializedConnections)
					{
						initializedConnections.Remove(connection as Connection.Tcp);
					}
				}

				if(connection.Channel != ushort.MaxValue)
				{
					lock(openedChannels)
					{
						Connection.Base[] connections;

						if(openedChannels.TryGetValue(connection.Remote, out connections))
						{
							connections[connection.Channel] = null;
						}
						openedChannels.Remove(connection.Remote);
					}
				}

				connection.Close();
			}
		}

		protected void DisconnectionHandler(Connection.Base connection)
		{
			Close(connection);

			if(state == State.RUNNING)
			{
				Reconnect(connection);
			}
		}

		protected void Drop(Connection.Tcp connection, string message, params object[] values)
		{
			string error_message = string.Format(message, values);

			if(!error_message.EndsWith("."))
			{
				error_message += ".";
			}

			error_message += " Dropping connection.";

			Debug.LogError(error_message);

			Close(connection);

			throw new DropException(error_message);
		}
		#endregion

		#region MonoBehaviour callbacks
		protected virtual void Awake()
		{
			state = State.STARTED;

			// Initialize singletons while in unity thread, if necessary
			Threads.APC.MonoBehaviourCall.Instance.GetType();
			Pattern.Factory<Message.Base, byte>.Initialize(Pattern.Factory.ByteConverter);

			serializer = new Binary();

			openedChannels = new Dictionary<Guid, Connection.Base[]>();

			initializedConnections = new HashSet<Connection.Tcp>();

			availablePorts = new HashSet<ushort>();

			foreach(PortRange range in openPorts)
			{
				if(availablePorts.Count < PortRange.maxPoolSize)
				{
					ushort start = Math.Min(range.minPort, range.maxPort);
					ushort end = Math.Max(range.minPort, range.maxPort);

					start = Math.Max(start, PortRange.minAvailablePort);
					end = Math.Min(end, PortRange.maxavailablePort);

					// Ok because start >= PortRange.minAvailablePort, i.e. > 0
					end = Math.Min(end, (ushort) (start + (PortRange.maxPoolSize - availablePorts.Count - 1)));

					for(ushort port = start; port <= end; port++)
					{
						availablePorts.Add(port);
					}
				}
			}
		}

		protected void OnDestroy()
		{
			Dispose(true);
		}

		protected virtual void OnValidate()
		{
			if(openPorts == null)
			{
				openPorts = new List<PortRange>();
			}

			if(openPorts.Count <= 0)
			{
				openPorts.Add(new PortRange());
			}

			foreach(PortRange range in openPorts)
			{
				if(range.minPort == 0 && range.maxPort == 0)
				{
					range.minPort = PortRange.minAvailablePort;
					range.maxPort = PortRange.maxavailablePort;
				}
			}
		}
		#endregion

		#region Public methods
		public Binary Serializer
		{
			get
			{
				return serializer;
			}
		}

		public bool Ready(Guid remote, ushort channel)
		{
			Connection.Base[] channels;
			bool result;

			lock(openedChannels)
			{
				result = state == State.RUNNING && openedChannels.TryGetValue(remote, out channels) && channel < channels.Length && channels[channel] != null && channels[channel].Connected();
			}

			return result;
		}

		public bool Ready(Guid remote)
		{
			Connection.Base[] channels;
			bool result;

			lock(openedChannels)
			{
				result = state == State.RUNNING && openedChannels.TryGetValue(remote, out channels) && channels.All(x => x != null && x.Connected());
			}

			return result;
		}

		public bool Ready()
		{
			bool result;

			lock(openedChannels)
			{
				result = state == State.RUNNING && openedChannels.All(p => p.Value.All(x => x != null && x.Connected()));
			}

			return result;
		}

		public void Send(Guid remote, ushort channel, byte[] data)
		{
			if(state == State.RUNNING)
			{
				Connection.Base[] client_channels;
				Connection.Base client_channel;

				lock(openedChannels)
				{
					if(!openedChannels.TryGetValue(remote, out client_channels))
					{
						throw new ArgumentException(string.Format("No connection with remote '{0}'. Nothing sent.", remote), "remote");
					}

					if(channel >= client_channels.Length || client_channels[channel] == null)
					{
						throw new ArgumentException(string.Format("Invalid channel. No channel with index '{0}'", channel), "channel");
					}

					client_channel = client_channels[channel];
				}

				client_channel.SendAsync(data);
			}
			else
			{
				Debug.LogWarningFormat("Can not send data when in state {0}. Nothing sent.", state);
			}
		}

		public void SendOthers(Guid remote, ushort channel, byte[] data)
		{
			if(state == State.RUNNING)
			{
				lock(openedChannels)
				{
					foreach(KeyValuePair<Guid, Connection.Base[]> pair in openedChannels)
					{
						if(remote == Guid.Empty || pair.Key != remote)
						{
							if(channel >= pair.Value.Length || pair.Value[channel] == null)
							{
								throw new ArgumentException(string.Format("Invalid channel. No channel with index '{0}'", channel), "channel");
							}

							pair.Value[channel].SendAsync(data);
						}
					}
				}
			}
			else
			{
				Debug.LogWarningFormat("Can not send data when in state {0}. Nothing sent.", state);
			}
		}

		public void SendAll(ushort channel, byte[] data)
		{
			SendOthers(Guid.Empty, channel, data);
		}

		protected void ReservePort(ushort port)
		{
			lock(availablePorts)
			{
				availablePorts.Remove(port);
			}
		}

		public void ReleasePort(ushort port)
		{
			lock(availablePorts)
			{
				availablePorts.Add(port);
			}
		}
		#endregion
	}

	public abstract class Base<T, U> : Base where T : MonitorChannel where U : Channel
	{
		#region Members
		public T negotiation;
		public List<U> channels;
		public Credentials credentials;
		#endregion

		#region Public methods
		public override ushort NbChannels
		{
			get
			{
				return (ushort) (channels != null ? channels.Count : 0);
			}
		}

		public override IEnumerable<Channel> Channels
		{
			get
			{
				return (IEnumerable<Channel>) channels;
			}
		}
		#endregion

		#region Shared network methods
		protected void SendMonitorCommand(Connection.Tcp connection, Message.Base message, uint message_size = 0)
		{
			Binary.Buffer buffer = serializer.GetBuffer(message_size != 0 ? message_size : 256);

			uint written = serializer.ToBytes(ref buffer, 0, Pattern.Factory<Message.Base, byte>.Get(message.GetType()));

			written += serializer.ToBytes(ref buffer, written, message);

			byte[] data = new byte[written];

			Array.Copy(buffer.Data, data, written);

			// Send the selected port. A value of 0 means that no port are available.
			connection.SendAsync(data);
		}

		protected Message.Base ReceiveMonitorCommand(byte[] data)
		{
			const ushort type_nb_bytes = 1;

			Message.Base message = Pattern.Factory<Message.Base, byte>.CreateInstance(data[0]);

			Binary.Buffer buffer = Serializer.GetBufferFromExistingData(data);

			uint read = Serializer.FromBytesOverwrite(buffer, type_nb_bytes, message);

			if(read != data.Length - type_nb_bytes)
			{
				Debug.LogErrorFormat("Some received data was not read. Read '{0}' bytes instead of '{1}'.", read, data.Length - type_nb_bytes);
			}

			return message;
		}

		protected UdpConnectionParams SendUdpParams(Connection.Tcp connection, Message.Negotiation.Parameters param)
		{
			bool success = false;

			UdpConnectionParams udp_param = new UdpConnectionParams
			{
				param = param,
				udp = null,
				remote = connection.GetRemoteAddress(),
				localPort = 0
			};

			Message.Negotiation.Channel.UDP msg = new Message.Negotiation.Channel.UDP
			{
				guid = param.guid,
				channel = param.channel,
				port = 0
			};

			if(channels != null && param.channel < channels.Count)
			{
				while(!success)
				{
					lock(availablePorts)
					{
						HashSet<ushort>.Enumerator it = availablePorts.GetEnumerator();

						if(it.MoveNext())
						{
							udp_param.localPort = it.Current;

							availablePorts.Remove(udp_param.localPort);
						}
						else
						{
							udp_param.localPort = 0;

							success = true;
						}
					}

					if(udp_param.localPort > 0)
					{
						try
						{
							udp_param.udp = new UdpClient(udp_param.localPort, AddressFamily.InterNetwork);

							ReservePort(udp_param.localPort);

							success = true;
						}
						catch(SocketException)
						{
							// Port unavailable. Remove it definitively from the list and try another port.
							udp_param.udp = null;

							udp_param.localPort = 0;

							success = false;
						}
					}
				}
			}

			msg.port = udp_param.localPort;

			SendMonitorCommand(connection, msg, Message.Negotiation.Channel.UDP.messageSize);

			return udp_param;
		}

		protected void ConnectUdp(UdpConnectionParams param, Message.Negotiation.Channel.UDP response)
		{ 
			if(param.udp != null && param.localPort > 0)
			{
				if(response.port > 0)
				{
					param.udp.Connect(param.remote, response.port);

					SaveChannel(new Connection.Udp(this, param.param, DisconnectionHandler, param.udp, param.localPort, response.port));
				}
				else
				{
					Debug.LogError("No available remote port for UDP connection.");
				}
			}
			else
			{
				Debug.LogError("No available local port for UDP connection.");
			}
	
		}

		protected void SaveMonitor(Connection.Tcp connection)
		{
			if(monitor == null)
			{
				monitor = connection;

				monitor.SetEvents(negotiation);
				monitor.SetEvents(OnMonitorReceive);

				monitor.Listen();
			}
		}

		protected void SaveChannel(Connection.Base connection)
		{
			// Remove initialized TCP connection from the pool of connections in initialization
			if(connection is Connection.Tcp)
			{
				lock(initializedConnections)
				{
					initializedConnections.Remove((Connection.Tcp) connection);
				}
			}

			if(connection.Channel < channels.Count)
			{
				Channel channel = channels[connection.Channel];

				// Save callbacks for the connection
				connection.SetEvents(channel);

				// Save the connection
				lock(openedChannels)
				{
					Connection.Base[] client_channels;

					if(!openedChannels.TryGetValue(connection.Remote, out client_channels))
					{
						client_channels = new Connection.Base[channels.Count];

						openedChannels.Add(connection.Remote, client_channels);
					}

					client_channels[connection.Channel] = connection;
				}

				Debug.LogFormat("{0} channel {1} on {2} success.", connection.GetType(), connection.Channel, connection.Remote);

				connection.Listen();
			}
			else
			{
				// No channel defined for this index. This should never happen as index are checked during port negotiation
				Debug.LogErrorFormat("No channel defined with index '{0}'.", connection.Channel);

				connection.Close();
			}
		}
		#endregion
	}
}

#endif //!NETFX_CORE
