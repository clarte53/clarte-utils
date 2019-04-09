#if !NETFX_CORE

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using UnityEngine;

namespace CLARTE.Net.Negotiation
{
    public class Server : Base<ServerChannel>
    {
        #region Members
        public const uint maxSupportedVersion = 1;

        public TextAsset certificate;
        public uint port;

        protected Threads.Thread listenerThread;
        protected TcpListener listener;
        protected X509Certificate2 serverCertificate;
        protected ManualResetEvent stopEvent;
        #endregion

        #region IDisposable implementation
        protected override void Dispose(bool disposing)
        {
            if(state != State.DISPOSED)
            {
                state = State.CLOSING;

                if(disposing)
                {
                    // TODO: delete managed state (managed objects).

                    listener.Stop();

                    stopEvent.Set();

                    CloseInitializedConnections();

                    CloseOpenedChannels();

                    listenerThread.Join();

                    stopEvent.Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                state = State.DISPOSED;
            }
        }
        #endregion

        #region MonoBehaviour callbacks
        protected override void Awake()
        {
            base.Awake();

            state = State.INITIALIZING;

            stopEvent = new ManualResetEvent(false);

            serverCertificate = null;

            // Should we use an encrypted channel?
            if(certificate != null && certificate.bytes.Length > 0)
            {
                string tmp_file = string.Format("{0}{1}{2}", Application.temporaryCachePath, Path.DirectorySeparatorChar, certificate.name);

                File.WriteAllBytes(tmp_file, certificate.bytes);

                try
                {
#if !UNITY_WSA
                    // Import the certificate
                    serverCertificate = new X509Certificate2(tmp_file);
#else
                    // At the moment, SslStream is not working on Hololens platform.
                    // Indeed, at the moment, player capabilities does not provide a way to authorize access to the trusted root certificates store.
                    throw new NotSupportedException("SSL streams are not supported on Hololens.");
#endif
                }
                catch(Exception)
                {
                    Debug.LogWarningFormat("Invalid certificate file '{0}'. Encryption is disabled.", certificate.name);

                    serverCertificate = null;
                }

                File.Delete(tmp_file);
            }

            listener = new TcpListener(IPAddress.Any, (int) port);
            listener.Start();

            listenerThread = new Threads.Thread(Listen);
            listenerThread.Start();

            Debug.LogFormat("Started server on port {0}", port);

            state = State.RUNNING;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if(channels != null)
            {
                foreach(ServerChannel channel in channels)
                {
                    if(channel.heartbeat == 0)
                    {
                        channel.type = Channel.Type.TCP;
                        channel.heartbeat = 2f;
                    }
                }
            }
        }
        #endregion

        #region Connection methods
        protected void Listen()
        {
            while(true)
            {
                // Listen for new connections
                IAsyncResult context = listener.BeginAcceptTcpClient(AcceptClient, null);

                // Wait for next connection or exit signal
                if(WaitHandle.WaitAny(new[] { stopEvent, context.AsyncWaitHandle }) == 0)
                {
                    return;
                }
            }
        }

        protected void AcceptClient(IAsyncResult async_result)
        {
            try
            {
                if(state == State.RUNNING)
                {
                    // Get the new connection
                    Connection.Tcp connection = new Connection.Tcp(listener.EndAcceptTcpClient(async_result), Guid.Empty, 0, defaultHeartbeat);

                    lock(initializedConnections)
                    {
                        initializedConnections.Add(connection);
                    }

                    connection.initialization = Threads.Tasks.Add(() => Connected(connection));
                }
            }
            catch(Exception exception)
            {
                Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
            }
        }

        protected void Connected(Connection.Tcp connection)
        {
            try
            {
                // We should be connected
                if(connection.client.Connected)
                {
                    // Get the stream associated with this connection
                    connection.stream = connection.client.GetStream();

                    // Send the protocol version
                    connection.Send(maxSupportedVersion);

                    if(connection.Receive(out connection.version))
                    {
                        if(connection.version < maxSupportedVersion)
                        {
                            Debug.LogWarningFormat("Client does not support protocol version '{0}'. Using version '{1}' instead.", maxSupportedVersion, connection.version);
                        }

                        // Notify the client if we will now switch on an encrypted channel
                        connection.Send(serverCertificate != null);

                        if(serverCertificate != null)
                        {
                            // Create the SSL wraping stream
                            connection.stream = new SslStream(connection.stream);

                            // Authenticate with the client
                            ((SslStream) connection.stream).BeginAuthenticateAsServer(serverCertificate, Authenticated, connection);
                        }
                        else
                        {
                            // No encryption, the channel stay as is
                            ValidateCredentials(connection);
                        }
                    }
                    else
                    {
                        Drop(connection, "Expected to receive negotiation protocol version.");
                    }
                }
                else
                {
                    Debug.LogError("The connection from the client failed.");
                }
            }
            catch(DropException)
            {
                throw;
            }
            catch(Exception exception)
            {
                Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
            }
        }

        protected void Authenticated(IAsyncResult async_result)
        {
            Connection.Tcp connection = null;

            try
            {
                // Finalize the authentication as server for the SSL stream
                connection = (Connection.Tcp) async_result.AsyncState;

                ((SslStream) connection.stream).EndAuthenticateAsServer(async_result);

                ValidateCredentials(connection);
            }
            catch(DropException)
            {
                throw;
            }
            catch(Exception)
            {
                Drop(connection, "Authentication failed.");
            }
        }

        protected void ValidateCredentials(Connection.Tcp connection)
        {
            string client_username;
            string client_password;

            // Get the client credentials
            if(connection.Receive(out client_username) && connection.Receive(out client_password))
            {
                // Check if the credentials are valid
                if(client_username == credentials.username && client_password == credentials.password)
                {
                    // Notify the client that the credentials are valid
                    connection.Send(true);

                    NegotiateChannels(connection);
                }
                else
                {
                    string error_message = string.Format("Invalid connection credentials for user '{0}'. Dropping connection.", client_username);

                    Debug.LogWarning(error_message);

                    // Notify the client that the credentials are wrong
                    connection.Send(false);

                    // Drop the connection
                    Close(connection);

                    throw new DropException(error_message);
                }
            }
            else
            {
                Drop(connection, "Expected to receive credentials.");
            }
        }

        protected void NegotiateChannels(Connection.Tcp connection)
        {
            bool negotiate;

            if(connection.Receive(out negotiate))
            {
                if(negotiate)
                {
                    // Send a new Guid for these connections
                    Guid remote = Guid.NewGuid();

                    connection.Send(remote.ToByteArray());

                    // Send channel description
                    ushort nb_channels = (ushort) Math.Min(channels != null ? channels.Count : 0, ushort.MaxValue);

                    connection.Send(nb_channels);

                    if(nb_channels <= 0)
                    {
                        Drop(connection, "No channels configured.");
                    }

                    for(ushort i = 0; i < nb_channels; i++)
                    {
                        ushort heartbeat = (ushort) (channels[i].heartbeat * 10f);

                        connection.Send((ushort) channels[i].type);
                        connection.Send(heartbeat);

                        if(channels[i].type == Channel.Type.UDP)
                        {
                            ConnectUdp(connection, remote, i, new TimeSpan(heartbeat * 100 * TimeSpan.TicksPerMillisecond));
                        }
                    }
                }
                else
                {
                    byte[] remote;
                    ushort channel;

                    if(connection.Receive(out remote))
                    {
                        if(connection.Receive(out channel))
                        {
							TimeSpan heartbeat = (
								channels[channel].heartbeat >= 0.1f ?
								new TimeSpan(((ushort) (channels[channel].heartbeat * 10f)) * 100 * TimeSpan.TicksPerMillisecond) :
								new TimeSpan(0, 0, 0, 0, -1)
							);

							connection.SetConfig(new Guid(remote), channel, heartbeat);

                            SaveChannel(connection);
                        }
                        else
                        {
                            Drop(connection, "Expected to receive channel index.");
                        }
                    }
                    else
                    {
                        Drop(connection, "Expected to receive connection identifier.");
                    }
                }
            }
            else
            {
                Drop(connection, "Expected to receive negotation flag.");
            }
        }
        #endregion
    }
}

#endif // !NETFX_CORE
