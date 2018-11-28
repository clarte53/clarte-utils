using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace CLARTE.Net.Negotiation
{
    public abstract class Base : MonoBehaviour, IDisposable
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

        #region Members
        protected static Threads.Tasks tasks;

        public List<PortRange> openPorts;

        protected Dictionary<IPAddress, Connection.Base[]> openedChannels;
        protected HashSet<Connection.Tcp> initializedConnections;
        protected HashSet<ushort> availablePorts;
        protected State state;
        #endregion

        #region Abstract methods
        protected abstract void Dispose(bool disposing);
        public abstract ushort NbChannels { get; }
        #endregion

        #region IDisposable implementation
        // TODO: replace finalizer only if the above Dispose(bool disposing) function as code to free unmanaged resources.
        ~Base()
        {
            Dispose(/*false*/);
        }

        /// <summary>
        /// Dispose of the HTTP server.
        /// </summary>
        public void Dispose()
        {
            // Pass true in dispose method to clean managed resources too and say GC to skip finalize in next line.
            Dispose(true);

            // If dispose is called already then say GC to skip finalize on this instance.
            // TODO: uncomment next line if finalizer is replaced above.
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Clean-up helpers
        protected void CloseInitializedConnections()
        {
            lock(initializedConnections)
            {
                foreach(Connection.Tcp connection in initializedConnections)
                {
                    if(connection.initialization != null)
                    {
                        connection.initialization.Wait();
                    }

                    connection.Close();
                }

                initializedConnections.Clear();
            }
        }

        protected void CloseOpenedChannels()
        {
            lock(openedChannels)
            {
                foreach(KeyValuePair<IPAddress, Connection.Base[]> pair in openedChannels)
                {
                    foreach(Connection.Base connection in pair.Value)
                    {
                        connection.Close();
                    }
                }

                openedChannels.Clear();
            }
        }

        protected void Close(Connection.Tcp connection)
        {
            if(connection != null)
            {
                lock(initializedConnections)
                {
                    initializedConnections.Remove(connection);
                }

                connection.Close();
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

            tasks = Threads.Tasks.Instance;

            Connection.Base.SetUnityThreadCall();

            openedChannels = new Dictionary<IPAddress, Connection.Base[]>();

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
        public bool Ready(IPAddress remote, ushort channel)
        {
            Connection.Base[] channels;

            return state == State.RUNNING && openedChannels.TryGetValue(remote, out channels) && channel < channels.Length && channels[channel] != null && channels[channel].Connected();
        }

        public bool Ready(IPAddress remote)
        {
            Connection.Base[] channels;

            return state == State.RUNNING && openedChannels.TryGetValue(remote, out channels) && channels.All(x => x != null && x.Connected());
        }

        public bool Ready()
        {
            return state == State.RUNNING && openedChannels.All(p => p.Value.All(x => x != null && x.Connected()));
        }

        public void Close()
        {
            Dispose();
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

    public abstract class Base<T> : Base where T : Channel
    {
        #region Members
        public List<T> channels;
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

        public void Send(IPAddress remote, ushort channel, byte[] data)
        {
            if(state == State.RUNNING)
            {
                Connection.Base[] client_channels;

                if(!openedChannels.TryGetValue(remote, out client_channels))
                {
                    throw new ArgumentException(string.Format("No connection with remote at {0}. Nothing sent.", remote), "remote");
                }

                if(channel >= client_channels.Length || client_channels[channel] == null)
                {
                    throw new ArgumentException(string.Format("Invalid channel. No channel with index '{0}'", channel), "channel");
                }

                client_channels[channel].SendAsync(data);
            }
            else
            {
                Debug.LogWarningFormat("Can not send data when in state {0}. Nothing sent.", state);
            }
        }

        public void SendOthers(IPAddress remote, ushort channel, byte[] data)
        {
            if(state == State.RUNNING)
            {
                foreach(KeyValuePair<IPAddress, Connection.Base[]> pair in openedChannels)
                {
                    if(remote == null || pair.Key != remote)
                    {
                        if(channel >= pair.Value.Length || pair.Value[channel] == null)
                        {
                            throw new ArgumentException(string.Format("Invalid channel. No channel with index '{0}'", channel), "channel");
                        }

                        pair.Value[channel].SendAsync(data);
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
            SendOthers(null, channel, data);
        }
        #endregion

        #region Shared network methods
        protected void ConnectUdp(Connection.Tcp connection, ushort channel)
        {
            UdpClient udp = null;

            ushort local_port = 0;
            ushort remote_port;

            bool success = false;

            if(channels != null && channel < channels.Count)
            {
                while(!success)
                {
                    lock(availablePorts)
                    {
                        HashSet<ushort>.Enumerator it = availablePorts.GetEnumerator();

                        if(it.MoveNext())
                        {
                            local_port = it.Current;

                            availablePorts.Remove(local_port);
                        }
                        else
                        {
                            local_port = 0;

                            success = true;
                        }
                    }

                    if(local_port > 0)
                    {
                        try
                        {
                            udp = new UdpClient(local_port, AddressFamily.InterNetwork);

                            success = true;
                        }
                        catch(SocketException)
                        {
                            // Port unavailable. Remove it definitively from the list and try another port.
                            udp = null;

                            success = false;
                        }
                    }
                }
            }

            // Send the selected port. A value of 0 means that no port are available.
            connection.Send(local_port);

            if(connection.Receive(out remote_port))
            {
                if(udp != null && local_port > 0)
                {
                    if(remote_port > 0)
                    {
                        udp.Connect(((IPEndPoint) connection.client.Client.RemoteEndPoint).Address, remote_port);

                        SaveChannel(new Connection.Udp(this, udp), channel);
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
            else
            {
                Drop(connection, "Expected to receive remote UDP port.");
            }
        }

        protected void SaveChannel(Connection.Base connection, ushort channel)
        {
            // Remove initialized TCP connection from the pool of connections in initialization
            if(connection is Connection.Tcp)
            {
                lock(initializedConnections)
                {
                    initializedConnections.Remove((Connection.Tcp) connection);
                }
            }

            IPAddress remote = connection.GetRemoteAddress();

            if(channel < channels.Count)
            {
                // Save callbacks for the connection
                connection.onConnected = channels[channel].onConnected;
                connection.onDisconnected = channels[channel].onDisconnected;
                connection.onReceive = channels[channel].onReceive;
                connection.channel = channel;

                // Save the connection
                lock(openedChannels)
                {
                    Connection.Base[] client_channels;

                    if(!openedChannels.TryGetValue(remote, out client_channels))
                    {
                        client_channels = new Connection.Base[channels.Count];

                        openedChannels.Add(remote, client_channels);
                    }

                    client_channels[channel] = connection;
                }

                Debug.LogFormat("{0} channel {1} success.", connection.GetType(), channel);

                connection.Listen();
            }
            else
            {
                // No channel defined for this index. This should never happen as index are checked during port negotiation
                Debug.LogErrorFormat("No channel defined with index '{0}'.", channel);

                connection.Close();
            }
        }
        #endregion
    }
}
