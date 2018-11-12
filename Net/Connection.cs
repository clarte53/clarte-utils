using System;
using System.IO;
using System.Net.Sockets;

namespace CLARTE.Net
{
    public abstract class Connection : IDisposable
    {
        #region Members
        protected bool disposed;
        #endregion

        #region Abstract methods
        protected abstract void Dispose(bool disposing);
        #endregion

        #region IDisposable implementation
        // TODO: replace finalizer only if the above Dispose(bool disposing) function as code to free unmanaged resources.
        ~Connection()
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

        #region Public methods
        public void Close()
        {
            Dispose();
        }
        #endregion

        #region Helper functions
        public static void SafeDispose<T>(T value) where T : IDisposable
        {
            try
            {
                if(value != null)
                {
                    value.Dispose();
                }
            }
            catch(ObjectDisposedException)
            {
                // Already done
            }
        }
        #endregion
    }

    public class TcpConnection : Connection
    {
        #region Members
        public Threads.Result initialization;
        public TcpClient client;
        public Stream stream;
        public uint version;
        #endregion

        #region Constructors
        public TcpConnection(TcpClient c)
        {
            client = c;
            stream = null;
        }
        #endregion

        #region IDisposable implementation
        protected override void Dispose(bool disposing)
        {
            if(!disposed)
            {
                if(disposing)
                {
                    // TODO: delete managed state (managed objects).

                    try
                    {
                        // Flush the stream to make sure that all sent data is effectively sent to the client
                        if(stream != null)
                        {
                            stream.Flush();
                        }
                    }
                    catch(ObjectDisposedException)
                    {
                        // Already closed
                    }

                    // Close the stream, the client and certificate (if any)
                    SafeDispose(stream);
                    SafeDispose(client);
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }
        #endregion
    }

    public class ClientTcpConnection : TcpConnection
    {
        #region Members
        public uint channel;
        #endregion

        #region Constructors
        public ClientTcpConnection(TcpClient client, uint channel) : base(client)
        {
            this.channel = channel;
        }
        #endregion
    }
}
