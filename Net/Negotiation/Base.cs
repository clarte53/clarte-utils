using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
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

        [StructLayout(LayoutKind.Explicit)]
        protected struct Converter
        {
            [FieldOffset(0)]
            public byte Byte1;

            [FieldOffset(1)]
            public byte Byte2;

            [FieldOffset(2)]
            public byte Byte3;

            [FieldOffset(3)]
            public byte Byte4;

            [FieldOffset(0)]
            public int Int;

            [FieldOffset(0)]
            public uint UInt;

            [FieldOffset(0)]
            public ushort UShort;

            public Converter(byte b1, byte b2, byte b3, byte b4)
            {
                Int = 0;
                UInt = 0;
                UShort = 0;
                Byte1 = b1;
                Byte2 = b2;
                Byte3 = b3;
                Byte4 = b4;
            }

            public Converter(int value)
            {
                Byte1 = 0;
                Byte2 = 0;
                Byte3 = 0;
                Byte4 = 0;
                UInt = 0;
                UShort = 0;
                Int = value;
            }

            public Converter(uint value)
            {
                Byte1 = 0;
                Byte2 = 0;
                Byte3 = 0;
                Byte4 = 0;
                Int = 0;
                UShort = 0;
                UInt = value;
            }

            public Converter(ushort value)
            {
                Byte1 = 0;
                Byte2 = 0;
                Byte3 = 0;
                Byte4 = 0;
                Int = 0;
                UInt = 0;
                UShort = value;
            }
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
        protected static readonly bool isLittleEndian;
        protected static Threads.Tasks tasks;

        public List<PortRange> openPorts;

        protected Dictionary<IPAddress, Connection.Base[]> openedChannels;
        protected HashSet<Connection.Tcp> initializedConnections;
        protected HashSet<ushort> availablePorts;
        protected State state;
        #endregion

        #region Abstract methods
        protected abstract void Dispose(bool disposing);
        #endregion

        #region Constructors
        static Base()
        {
            isLittleEndian = BitConverter.IsLittleEndian;
        }
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

        #region Helper serialization functions
        protected void Send(Connection.Tcp connection, bool value)
        {
            connection.stream.WriteByte(value ? (byte) 1 : (byte) 0);
        }

        protected void Send(Connection.Tcp connection, ushort value)
        {
            Converter c = new Converter(value);

            if(isLittleEndian)
            {
                connection.stream.WriteByte(c.Byte2);
                connection.stream.WriteByte(c.Byte1);
            }
            else
            {
                connection.stream.WriteByte(c.Byte1);
                connection.stream.WriteByte(c.Byte2);
            }
        }

        protected void Send(Connection.Tcp connection, int value)
        {
            Converter c = new Converter(value);

            if(isLittleEndian)
            {
                connection.stream.WriteByte(c.Byte4);
                connection.stream.WriteByte(c.Byte3);
                connection.stream.WriteByte(c.Byte2);
                connection.stream.WriteByte(c.Byte1);
            }
            else
            {
                connection.stream.WriteByte(c.Byte1);
                connection.stream.WriteByte(c.Byte2);
                connection.stream.WriteByte(c.Byte3);
                connection.stream.WriteByte(c.Byte4);
            }
        }

        protected void Send(Connection.Tcp connection, uint value)
        {
            Send(connection, new Converter(value).Int);
        }

        protected void Send(Connection.Tcp connection, byte[] data)
        {
            Send(connection, data.Length);

            connection.stream.Write(data, 0, data.Length);
        }

        protected void Send(Connection.Tcp connection, string data)
        {
            Send(connection, Encoding.UTF8.GetBytes(data));
        }

        protected bool Receive(Connection.Tcp connection, out bool value)
        {
            int val = connection.stream.ReadByte();

            value = false;

            if(val >= 0)
            {
                value = (val > 0);

                return true;
            }

            return false;
        }

        protected bool Receive(Connection.Tcp connection, out ushort value)
        {
            int v1, v2;

            value = 0;

            if(isLittleEndian)
            {
                v2 = connection.stream.ReadByte();
                v1 = connection.stream.ReadByte();
            }
            else
            {
                v1 = connection.stream.ReadByte();
                v2 = connection.stream.ReadByte();
            }

            if(v1 >= 0 && v2 >= 0)
            {
                Converter c = new Converter((byte) v1, (byte) v2, 0, 0);

                value = c.UShort;

                return true;
            }

            return false;
        }

        protected bool Receive(Connection.Tcp connection, out int value)
        {
            int v1, v2, v3, v4;

            value = 0;

            if(isLittleEndian)
            {
                v4 = connection.stream.ReadByte();
                v3 = connection.stream.ReadByte();
                v2 = connection.stream.ReadByte();
                v1 = connection.stream.ReadByte();
            }
            else
            {
                v1 = connection.stream.ReadByte();
                v2 = connection.stream.ReadByte();
                v3 = connection.stream.ReadByte();
                v4 = connection.stream.ReadByte();
            }

            if(v1 >= 0 && v2 >= 0 && v3 >= 0 && v4 >= 0)
            {
                Converter c = new Converter((byte) v1, (byte) v2, (byte) v3, (byte) v4);

                value = c.Int;

                return true;
            }

            return false;
        }

        protected bool Receive(Connection.Tcp connection, out uint value)
        {
            int val;

            bool result = Receive(connection, out val);

            value = new Converter(val).UInt;

            return result;
        }

        protected bool Receive(Connection.Tcp connection, out byte[] data)
        {
            int size;

            data = null;

            if(Receive(connection, out size))
            {
                data = new byte[size];

                if(connection.stream.Read(data, 0, size) == size)
                {
                    return true;
                }
                else
                {
                    data = null;
                }
            }

            return false;
        }

        protected bool Receive(Connection.Tcp connection, out string data)
        {
            byte[] raw_data;

            data = null;

            if(Receive(connection, out raw_data))
            {
                data = Encoding.UTF8.GetString(raw_data);

                return true;
            }

            return false;
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

                client_channels[channel].Send(data);
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

                        pair.Value[channel].Send(data);
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
                            udp = new UdpClient(local_port, AddressFamily.InterNetworkV6);

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
            Send(connection, local_port);

            if(Receive(connection, out remote_port))
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
                // Save receive callback to the connection
                connection.onReceive = channels[channel].onReceive;

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
