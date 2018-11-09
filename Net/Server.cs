using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace CLARTE.Net
{
    public class Server : Base, IDisposable
    {
        protected class Worker
        {
            #region Members
            public Threads.Thread thread;
            public TcpClient client;
            public Stream stream;
            public X509Certificate2 certificate;
            #endregion

            #region Constructors
            public Worker(TcpClient c)
            {
                thread = null;
                client = c;
                stream = client.GetStream();
                certificate = null;
            }
            #endregion
        }

        #region Members
        public uint port;
        public string certificate;
        public Credentials credentials;
        public List<uint> openPorts;
        public List<ServerChannel> channels;

        protected Serialization.Binary serializer;
        protected Threads.Thread mainThread;
        protected HashSet<Worker> workers;
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
                        foreach(Worker worker in workers)
                        {
                            SafeDispose(worker.stream);
                            SafeDispose(worker.client);
                            SafeDispose(worker.certificate);

                            if(worker.thread != null)
                            {
                                worker.thread.Join();
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

            workers = new HashSet<Worker>();

            listener = new TcpListener(IPAddress.Any, (int) port);
            listener.Start();

            mainThread = new Threads.Thread(Listen);
            mainThread.Start();
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
                Worker worker = new Worker(client);

                worker.thread = new Threads.Thread(() => Connected(worker));
                worker.thread.Start();

                lock(workers)
                {
                    workers.Add(worker);
                }
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogError(exception);
            }
        }

        protected void Connected(Worker worker)
        {
            // We should be connected
            if(worker.client.Connected)
            {
                // Should we use an encrypted channel?
                bool encrypted = !string.IsNullOrEmpty(certificate) && File.Exists(certificate);

                if(encrypted)
                {
                    try
                    {
                        // Import the certificate
                        worker.certificate = new X509Certificate2();

                        worker.certificate.Import(certificate);
                    }
                    catch(Exception)
                    {
                        UnityEngine.Debug.LogWarningFormat("Invalid certificate file '{0}'. Encryption is disabled.", certificate);

                        if(worker.certificate != null)
                        {
                            worker.certificate.Dispose();
                        }

                        worker.certificate = null;

                        encrypted = false;
                    }
                }

                // Notify the client if we will now switch on an encrypted channel
                Send(worker.stream, encrypted);

                if(encrypted)
                {
                    // Create the SSL wraping stream
                    worker.stream = new SslStream(worker.stream);

                    ((SslStream) worker.stream).BeginAuthenticateAsServer(worker.certificate, EncryptConnection, worker);
                }
                else
                {
                    // No encryption, the channel stay as is
                    Secured(worker);
                }
            }
        }

        protected void EncryptConnection(IAsyncResult async_result)
        {
            // Finalize the authentication as server for the SSL stream
            Worker worker = (Worker) async_result.AsyncState;

            ((SslStream) worker.stream).EndAuthenticateAsServer(async_result);

            Secured(worker);
        }

        protected void Secured(Worker worker)
        {
            byte[] raw_username;
            byte[] raw_password;

            // Get the client credentials
            if(Receive(worker.stream, out raw_username) && Receive(worker.stream, out raw_password))
            {
                // Check if the credentials are valid
                if(Encoding.UTF8.GetString(raw_username) == credentials.username && Encoding.UTF8.GetString(raw_password) == credentials.password)
                {
                    // Notify the client that the credentials are valid
                    Send(worker.stream, true);


                }
                else
                {
                    UnityEngine.Debug.LogWarningFormat("Invalid connection credentials for user '{0}'. Dropping connection.", Encoding.UTF8.GetString(raw_username));

                    // Notify the client that the credentials are wrong
                    Send(worker.stream, false);

                    // Drop the connection
                    Close(worker);

                    return;
                }
            }
        }

        protected void Close(Worker worker)
        {
            lock(workers)
            {
                // Remove this worker from the pool of current workers
                workers.Remove(worker);
            }

            try
            {
                // Flush the stream to make sure that all sent data is effectively sent to the client
                if(worker.stream != null)
                {
                    worker.stream.Flush();
                }
            }
            catch(ObjectDisposedException)
            {
                // Already closed
            }

            // Close the stream, the client and certificate (if any)
            SafeDispose(worker.stream);
            SafeDispose(worker.client);
            SafeDispose(worker.certificate);
        }
        #endregion
    }
}
