using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace CLARTE.Net
{
    public class Server : Base
    {
        #region Members
        public const uint maxSupportedVersion = 1;

        public uint port;
        public string certificate;
        public Credentials credentials;
        public List<uint> openPorts;
        public List<ServerChannel> channels;

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

                    foreach(Channel channel in channels)
                    {
                        if(channel != null)
                        {
                            channel.Close();
                        }
                    }

                    Connection.SafeDispose(serverCertificate);

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
            if(!string.IsNullOrEmpty(certificate))
            {
                try
                {
                    // Import the certificate
                    serverCertificate = new X509Certificate2();

                    serverCertificate.Import(certificate);
                }
                catch(Exception)
                {
                    UnityEngine.Debug.LogWarningFormat("Invalid certificate file '{0}'. Encryption is disabled.", certificate);

                    if(serverCertificate != null)
                    {
                        serverCertificate.Dispose();
                    }

                    serverCertificate = null;
                }
            }

            listener = new TcpListener(IPAddress.IPv6Any, (int) port);
            listener.Start();

            listenerThread = new Threads.Thread(Listen);
            listenerThread.Start();

            UnityEngine.Debug.LogFormat("Started server on port {0}", port);

            state = State.RUNNING;
        }

        protected void OnDestroy()
        {
            Dispose();
        }
        #endregion

        #region Public methods
        public void Send(uint channel, byte[] data)
        {
            if(state == State.RUNNING)
            {
                if(channels == null || channel >= channels.Count || channels[(int) channel] == null)
                {
                    throw new ArgumentException(string.Format("Invalid channel. No channel with index '{0}'", channel), "channel");
                }

                channels[(int) channel].Send(data);
            }
            else
            {
                UnityEngine.Debug.LogWarningFormat("Can not send data when in state {0}. Nothing sent.", state);
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
                    TCPConnection connection = new TCPConnection(listener.EndAcceptTcpClient(async_result));

                    lock(initializedConnections)
                    {
                        initializedConnections.Add(connection);
                    }

                    connection.initialization = tasks.Add(() => Connected(connection));
                }
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
            }
        }

        protected void Connected(TCPConnection connection)
        {
            try
            {
                // We should be connected
                if(connection.client.Connected)
                {
                    // Get the stream associated with this connection
                    connection.stream = connection.client.GetStream();

                    // Send the protocol version
                    Send(connection, maxSupportedVersion);

                    if(Receive(connection, out connection.version))
                    {
                        if(connection.version < maxSupportedVersion)
                        {
                            UnityEngine.Debug.LogWarningFormat("Client does not support protocol version '{0}'. Using version '{1}' instead.", maxSupportedVersion, connection.version);
                        }

                        // Notify the client if we will now switch on an encrypted channel
                        Send(connection, serverCertificate != null);

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
                        Drop(connection, "Expected to receive negotiation protocol version. Dropping connection.");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError("The connection from the client failed.");
                }
            }
            catch(DropException)
            {
                return;
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
            }
        }

        protected void Authenticated(IAsyncResult async_result)
        {
            TCPConnection connection = null;

            try
            {
                // Finalize the authentication as server for the SSL stream
                connection = (TCPConnection) async_result.AsyncState;

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

        protected void ValidateCredentials(TCPConnection connection)
        {
            string client_username;
            string client_password;

            // Get the client credentials
            if(Receive(connection, out client_username) && Receive(connection, out client_password))
            {
                // Check if the credentials are valid
                if(client_username == credentials.username && client_password == credentials.password)
                {
                    // Notify the client that the credentials are valid
                    Send(connection, true);

                    //TODO
                    UnityEngine.Debug.Log("Success");
                }
                else
                {
                    UnityEngine.Debug.LogWarningFormat("Invalid connection credentials for user '{0}'. Dropping connection.", client_username);

                    // Notify the client that the credentials are wrong
                    Send(connection, false);

                    // Drop the connection
                    Close(connection);

                    throw new DropException();
                }
            }
            else
            {
                Drop(connection, "Expected to receive credentials. Dropping connection.");
            }
        }
        #endregion
    }
}
