#if !NETFX_CORE

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
		protected IPAddress address;
		protected Guid remote;
        protected ushort channel;
        protected TimeSpan heartbeat;
		protected bool autoReconnect;
		protected Action<Base> disconnectionHandler;
        protected Events.ConnectionCallback onConnected;
        protected Events.DisconnectionCallback onDisconnected;
		protected Events.ExceptionCallback onException;
        protected Events.ReceiveCallback onReceive;
        protected Events.ReceiveProgressCallback onReceiveProgress;
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
        public Base(Guid remote, ushort channel, TimeSpan heartbeat, bool auto_reconnect, Action<Base> disconnection_handler)
        {
			SetConfig(remote, channel, heartbeat);

			autoReconnect = auto_reconnect;
			disconnectionHandler = disconnection_handler;

			sendResult = null;

            sendQueue = new Queue<Threads.Task>();

            stopEvent = new ManualResetEvent(false);
            addEvent = new ManualResetEvent(false);

            worker = new Threads.Thread(Worker);
        }
		#endregion

		#region Getter / Setter
		public IPAddress Address
		{
			get
			{
				return address;
			}
		}

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

		public TimeSpan Heartbeat
		{
			get
			{
				return heartbeat;
			}
		}

		public bool AutoReconnect
		{
			get
			{
				return autoReconnect;
			}
		}
		#endregion

		#region IDisposable implementation
		protected void Dispose(bool disposing)
        {
            if(!disposed)
            {
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

				Threads.APC.MonoBehaviourCall.Instance.Call(() =>
				{
					if(disconnectionHandler != null)
					{
						disconnectionHandler(this);
					}

					if(onDisconnected != null)
					{
						onDisconnected.Invoke(address, remote, channel);
					}
				});
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

        public void SetEvents(Events.ConnectionCallback on_connected, Events.DisconnectionCallback on_disconnected, Events.ExceptionCallback on_exception, Events.ReceiveCallback on_receive, Events.ReceiveProgressCallback on_receive_progress)
        {
            onConnected = on_connected;
            onDisconnected = on_disconnected;
			onException = on_exception;
            onReceive = on_receive;
            onReceiveProgress = on_receive_progress;
        }

        public void Close()
        {
			if(!disposed)
			{
				Dispose();
			}
        }

        public void Listen()
        {
            if(!listen)
            {
                listen = true;

				address = GetRemoteAddress();

				ReceiveAsync();

                worker.Start();
            }

            Threads.APC.MonoBehaviourCall.Instance.Call(() => onConnected.Invoke(address, remote, channel));
        }

        public void SendAsync(byte[] data)
        {
            lock(sendQueue)
            {
                Threads.Result result = CreateResult();

                sendQueue.Enqueue(new Threads.Task(() => SendAsync(result, data), result));
            }

            lock(addEvent)
            {
                addEvent.Set();
            }
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

		protected Threads.Result CreateResult()
		{
			return new Threads.Result(e =>
			{
				lock(addEvent)
				{
					addEvent.Set();
				}

				HandleException(e);
			});
		}

		protected void HandleException(Exception e)
		{
			if(e != null)
			{
				Type type = e.GetType();

				if(typeof(System.IO.IOException).IsAssignableFrom(type) || typeof(System.Net.Sockets.SocketException).IsAssignableFrom(type))
				{
					Close();
				}
				else if(typeof(ObjectDisposedException).IsAssignableFrom(type))
				{
					// Nothing to do, the connection was closed and the receiving methods are shutting down
				}
				else
				{
					Threads.APC.MonoBehaviourCall.Instance.Call(() => onException.Invoke(address, remote, channel, e));
				}
			}
		}
        #endregion

        #region Thread background worker
        protected void Worker()
        {
            WaitHandle[] wait = new WaitHandle[] { stopEvent, addEvent };

			byte[] heartbeat_data = new byte[0];

			int event_idx = 0;

            while((event_idx = WaitHandle.WaitAny(wait, heartbeat)) != 0)
            {
                if(event_idx == WaitHandle.WaitTimeout)
                {
					// Handle heartbeat
					if(sendResult == null || sendResult.Done)
					{
						sendResult = CreateResult();

						SendAsync(sendResult, heartbeat_data);
					}
				}
                else
                {
                    Threads.Task task = null;

                    lock(addEvent)
                    {
                        if(sendResult == null || sendResult.Done)
                        {
                            sendResult = null;

                            lock(sendQueue)
                            {
                                if(sendQueue.Count > 0)
                                {
                                    task = sendQueue.Dequeue();
                                }
                                else
                                {
                                    // Nothing to do anymore, go to sleep
                                    addEvent.Reset();
                                }
                            }
                        }
                        else
                        {
                            // Not done yet, go to sleep
                            addEvent.Reset();
                        }
                    }

                    if(task != null)
                    {
                        sendResult = (Threads.Result) task.result;

                        task.callback();
                    }
                }
            }
        }
        #endregion
    }
}

#endif // !NETFX_CORE
