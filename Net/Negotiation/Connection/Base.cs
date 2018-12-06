using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

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
        protected Guid remote;
        protected ushort channel;
        protected TimeSpan heartbeat;
        protected Events.ConnectionCallback onConnected;
        protected Events.DisconnectionCallback onDisconnected;
        protected Events.ReceiveCallback onReceive;
        protected ManualResetEvent stopEvent;
        protected ManualResetEvent addEvent;
        protected Threads.Thread worker;
        protected Threads.Result sendResult;
        protected Queue<Threads.Task> sendQueue;
        protected bool listen;
        private bool disposed;
        #endregion

        #region Abstract methods
        protected abstract void DisposeInternal(bool disposing);
        public abstract IPAddress GetRemoteAddress();
        public abstract bool Connected();
        protected abstract void SendAsync(Threads.Result result, byte[] data);
        protected abstract void ReceiveAsync();
        #endregion

        #region Constructors
        public Base(Guid remote, ushort channel, TimeSpan heartbeat)
        {
            this.remote = remote;
            this.channel = channel;
            this.heartbeat = heartbeat;

            sendResult = null;

            sendQueue = new Queue<Threads.Task>();

            stopEvent = new ManualResetEvent(false);
            addEvent = new ManualResetEvent(false);

            worker = new Threads.Thread(Worker);
        }
        #endregion

        #region Getter / Setter
        public Guid Remote
        {
            get
            {
                return remote;
            }
        }

        public ushort Channel
        {
            get
            {
                return channel;
            }
        }
        #endregion

        #region IDisposable implementation
        protected void Dispose(bool disposing)
        {
            if(!disposed)
            {
                Threads.APC.MonoBehaviourCall.Instance.Call(() => onDisconnected.Invoke(GetRemoteAddress(), remote, channel));

                DisposeInternal(disposing);

                if(disposing)
                {
                    // TODO: delete managed state (managed objects).
                    stopEvent.Set();

                    if(listen)
                    {
                        worker.Join();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }

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
        public void SetConfig(Guid remote, ushort channel, TimeSpan heartbeat)
        {
            this.remote = remote;
            this.channel = channel;
            this.heartbeat = heartbeat;
        }

        public void SetEvents(Events.ConnectionCallback on_connected, Events.DisconnectionCallback on_disconnected, Events.ReceiveCallback on_receive)
        {
            onConnected = on_connected;
            onDisconnected = on_disconnected;
            onReceive = on_receive;
        }

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

                worker.Start();
            }

            Threads.APC.MonoBehaviourCall.Instance.Call(() => onConnected.Invoke(GetRemoteAddress(), remote, channel));
        }

        public void SendAsync(byte[] data)
        {
            Threads.Result result = new Threads.Result(() => addEvent.Set());

            Threads.Task task = new Threads.Task(() => SendAsync(result, data), result);

            lock(sendQueue)
            {
                sendQueue.Enqueue(task);
            }

            addEvent.Set();
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

        #region Thread background worker
        protected void Worker()
        {
            WaitHandle[] wait = new WaitHandle[] { stopEvent, addEvent };

            int event_idx = 0;

            while((event_idx = WaitHandle.WaitAny(wait, heartbeat)) != 0)
            {
                if(event_idx == WaitHandle.WaitTimeout)
                {
                    // Handle heartbeat

                    //TODO
                }
                else
                {
                    Threads.Task task = null;

                    lock(sendQueue)
                    {
                        if(sendQueue.Count > 0 && (sendResult == null || sendResult.Done))
                        {
                            task = sendQueue.Dequeue();
                        }
                        else
                        {
                            sendResult = null;

                            // Nothing to do anymore, go to sleep
                            addEvent.Reset();
                        }
                    }

                    if(task != null)
                    {
                        sendResult = task.result;

                        task.callback();
                    }
                }
            }
        }
        #endregion
    }
}
