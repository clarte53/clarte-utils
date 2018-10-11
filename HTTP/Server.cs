using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

#if UNITY_WSA && !UNITY_EDITOR
// On UWP platforms, threads are not available. Therefore, we need support for Tasks, i.e. .Net version >= 4
using MyThread = System.Threading.Tasks.Task;
#else
using MyThread = System.Threading.Thread;
#endif

namespace CLARTE.HTTP
{
    public class Server : IDisposable
    {
        #region Members
        private readonly HttpListener listener;
        private readonly MyThread listenerWorker;
        private readonly ManualResetEvent stopEvent;
        private bool disposed;
        #endregion

        #region Constructors
        public Server(ushort port)
        {
            if(!HttpListener.IsSupported)
            {
                throw new NotSupportedException("HTTP server is not support on this implementation.");
            }

            Threads.Tasks.Instance.GetType(); // To initialize unity objects in unity thread

            stopEvent = new ManualResetEvent(false);

            listener = new HttpListener();
            listener.Prefixes.Add(string.Format("http://*:{0}/", port));
            listener.Start();

#if UNITY_WSA && !UNITY_EDITOR
            listenerWorker = new MyThread(Listen, System.Threading.Tasks.TaskCreationOptions.LongRunning);
#else
            listenerWorker = new MyThread(Listen);
#endif
            listenerWorker.Start();
        }
        #endregion

        #region IDisposable implementation
        protected virtual void Dispose(bool disposing)
        {
            if(!disposed)
            {
                if(disposing)
                {
                    // TODO: delete managed state (managed objects).

                    stopEvent.Set();

#if UNITY_WSA && !UNITY_EDITOR
                    listenerWorker.Wait();
#else
                    listenerWorker.Join();
#endif

                    listener.Stop();

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

        #region Control methods
        public void Stop()
        {
            Dispose();
        }
        #endregion

        #region Thread methods
        private void Listen()
        {
            while(listener.IsListening)
            {
                IAsyncResult context = listener.BeginGetContext(Receive, null);

                if(WaitHandle.WaitAny(new[] { stopEvent, context.AsyncWaitHandle }) == 0)
                {
                    return;
                }
            }
        }

        private void Receive(IAsyncResult async_result)
        {
            try
            {
                HttpListenerContext context = listener.EndGetContext(async_result);

                Threads.Tasks.Instance.Add(() => Send(context));
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogError(exception);
            }
        }

        private void Send(HttpListenerContext context)
        {
            try
            {
                Uri url = ReceiveRequest(context);

                if(url != null)
                {
                    SendResponse(context, url);
                }
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogError(exception);
            }
        }
        #endregion

        #region HTTP handling
        private Uri ReceiveRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;

            UnityEngine.Debug.LogFormat("{0} {1}", request.HttpMethod, request.Url);
            Uri url = request.Url;

            UnityEngine.Debug.Log("Headers:");
            System.Collections.Specialized.NameValueCollection headers = request.Headers;
            for(int i = 0; i < headers.Count; i++)
            {
                UnityEngine.Debug.LogFormat("{0}: {1}", headers.GetKey(i), headers.Get(i));
            }

            int request_size = (int) request.ContentLength64;
            byte[] buffer_input = new byte[request_size];

            Stream input = request.InputStream;
            input.Read(buffer_input, 0, request_size);
            input.Close();

            UnityEngine.Debug.LogFormat("Data:\n{0}", Encoding.UTF8.GetString(buffer_input));

            return url;
        }

        private void SendResponse(HttpListenerContext context, Uri url)
        {
            const string hello_world = "<HTML><BODY> Hello world!</BODY></HTML>";

            SendResponse(context.Response, hello_world);
        }

        private void SendResponse(HttpListenerResponse response, string data)
        {
            byte[] buffer_output = Encoding.UTF8.GetBytes(data);

            response.ContentLength64 = buffer_output.Length;

            Stream output = response.OutputStream;
            output.Write(buffer_output, 0, buffer_output.Length);
            output.Close();
        }
        #endregion
    }
}
