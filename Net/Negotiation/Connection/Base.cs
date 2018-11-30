using System;
using System.Net;

namespace CLARTE.Net.Negotiation.Connection
{
    public abstract class Base : IDisposable
    {
        protected struct SendState
        {
            public Threads.Result result;
            public byte[] data;
        }

        protected struct ReceiveState
        {
            public IPEndPoint ip;
            public byte[] data;
            public int offset;

            public ReceiveState(IPEndPoint ip)
            {
                this.ip = ip;

                data = null;
                offset = 0;
            }

            public void Set(byte[] data)
            {
                this.data = data;

                offset = 0;
            }

            public int MissingDataLength
            {
                get
                {
                    return data != null ? data.Length - offset : 0;
                }
            }
        }

        #region Members
        public Events.ConnectionCallback onConnected;
        public Events.DisconnectionCallback onDisconnected;
        public Events.ReceiveCallback onReceive;
        public ushort? channel;

        protected bool listen;
        protected bool disposed;
        #endregion

        #region Abstract methods
        protected abstract void Dispose(bool disposing);
        public abstract IPAddress GetRemoteAddress();
        public abstract bool Connected();
        public abstract Threads.Result SendAsync(byte[] data);
        protected abstract void ReceiveAsync();
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

        #region Public methods
        public void Close()
        {
            Dispose();
        }

        public void Listen()
        {
            if(!listen)
            {
                listen = true;

                ReceiveAsync();
            }

            Threads.APC.MonoBehaviourCall.Instance.Call(() => onConnected.Invoke(GetRemoteAddress(), channel.HasValue ? channel.Value : (ushort) 0));
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
}
