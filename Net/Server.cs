using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace CLARTE.Net
{
    public class Server : Base, IDisposable
    {
        #region Members
        public uint port;
        public string certificate;
        public Credentials credentials;
        public List<uint> openPorts;
        public List<ServerChannel> channels;

        protected Serialization.Binary serializer;
        protected Threads.Thread mainThread;
        protected HashSet<ServerConnection> workers;
        protected TcpListener listener;
        protected ManualResetEvent stopEvent;
        protected bool disposed;
        #endregion

        #region IDisposable implementation
        protected virtual void Dispose(bool disposing)
        {
            if(!disposed)
            {
                if(disposing)
                {
                    // TODO: delete managed state (managed objects).

                    listener.Stop();

                    stopEvent.Set();

                    lock(workers)
                    {
                        foreach(ServerConnection connection in workers)
                        {
                            SafeDispose(connection.stream);
                            SafeDispose(connection.client);
                            SafeDispose(connection.certificate);

                            if(connection.thread != null)
                            {
                                connection.thread.Join();
                            }
                        }

                        workers.Clear();
                    }

                    mainThread.Join();

                    stopEvent.Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }

        // TODO: replace finalizer only if the above Dispose(bool disposing) function as code to free unmanaged resources.
        ~Server()
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

        #region MonoBehaviour callbacks
        protected void Awake()
        {
            serializer = new Serialization.Binary();

            stopEvent = new ManualResetEvent(false);

            workers = new HashSet<ServerConnection>();

            listener = new TcpListener(IPAddress.Any, (int) port);
            listener.Start();

            mainThread = new Threads.Thread(Listen);
            mainThread.Start();

            UnityEngine.Debug.LogFormat("Started server on port {0}", port);
        }

        protected void OnDestroy()
        {
            Dispose();
        }
        #endregion

        #region Public methods
        public void Send(uint channel, byte[] data)
        {
            if(channels == null || channel >= channels.Count || channels[(int) channel] == null)
            {
                throw new ArgumentException(string.Format("Invalid channel. No channel with index '{0}'", channel), "channel");
            }

            channels[(int) channel].Send(data);
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
                // Get the new connection
                TcpClient client = listener.EndAcceptTcpClient(async_result);

                // Create a worker thread for this connection
                ServerConnection connection = new ServerConnection(client);

                connection.thread = new Threads.Thread(() => Connected(connection));
                connection.thread.Start();

                lock(workers)
                {
                    workers.Add(connection);
                }
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogError(exception);
            }
        }

        protected void Connected(ServerConnection connection)
        {
            // We should be connected
            if(connection.client.Connected)
            {
                // Get the stream associated with this connection
                connection.stream = connection.client.GetStream();

                // Should we use an encrypted channel?
                bool encrypted = !string.IsNullOrEmpty(certificate) && File.Exists(certificate);

                if(encrypted)
                {
                    try
                    {
                        // Import the certificate
                        connection.certificate = new X509Certificate2();

                        connection.certificate.Import(certificate);
                    }
                    catch(Exception)
                    {
                        UnityEngine.Debug.LogWarningFormat("Invalid certificate file '{0}'. Encryption is disabled.", certificate);

                        if(connection.certificate != null)
                        {
                            connection.certificate.Dispose();
                        }

                        connection.certificate = null;

                        encrypted = false;
                    }
                }

                // Notify the client if we will now switch on an encrypted channel
                Send(connection.stream, encrypted);

                if(encrypted)
                {
                    // Create the SSL wraping stream
                    connection.stream = new SslStream(connection.stream);

                    // Authenticate with the client
                    ((SslStream) connection.stream).BeginAuthenticateAsServer(connection.certificate, Authenticated, connection);
                }
                else
                {
                    // No encryption, the channel stay as is
                    ValidateCredentials(connection);
                }
            }

            else
            {
                UnityEngine.Debug.LogError("The connection from the client failed.");
            }
        }

        protected void Authenticated(IAsyncResult async_result)
        {
            // Finalize the authentication as server for the SSL stream
            ServerConnection connection = (ServerConnection) async_result.AsyncState;

            ((SslStream) connection.stream).EndAuthenticateAsServer(async_result);

            ValidateCredentials(connection);
        }

        protected void ValidateCredentials(ServerConnection connection)
        {
            string client_username;
            string client_password;

            // Get the client credentials
            if(Receive(connection.stream, out client_username) && Receive(connection.stream, out client_password))
            {
                // Check if the credentials are valid
                if(client_username == credentials.username && client_password == credentials.password)
                {
                    // Notify the client that the credentials are valid
                    Send(connection.stream, true);

                    //TODO
                    UnityEngine.Debug.Log("Success");
                }
                else
                {
                    UnityEngine.Debug.LogWarningFormat("Invalid connection credentials for user '{0}'. Dropping connection.", client_username);

                    // Notify the client that the credentials are wrong
                    Send(connection.stream, false);

                    // Drop the connection
                    Close(connection);

                    return;
                }
            }
            else
            {
                UnityEngine.Debug.LogError("Expected to receive credentials. Dropping connection.");

                // Drop the connection
                Close(connection);

                return;
            }
        }

        protected void Close(ServerConnection connection)
        {
            lock(workers)
            {
                // Remove this worker from the pool of current workers
                workers.Remove(connection);
            }

            try
            {
                // Flush the stream to make sure that all sent data is effectively sent to the client
                if(connection.stream != null)
                {
                    connection.stream.Flush();
                }
            }
            catch(ObjectDisposedException)
            {
                // Already closed
            }

            // Close the stream, the client and certificate (if any)
            SafeDispose(connection.stream);
            SafeDispose(connection.client);
            SafeDispose(connection.certificate);
        }
        #endregion
    }
}
