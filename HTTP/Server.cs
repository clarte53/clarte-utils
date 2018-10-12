using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;


namespace CLARTE.HTTP
{
    public class Server : IDisposable
    {
        public struct Response
        {
            public string mimeType;
            public byte[] data;

            public Response(string mime_type, byte[] output_data)
            {
                mimeType = mime_type;
                data = output_data;
            }
        }

        #region Delegates
        public delegate Response Endpoint(Dictionary<string, string> parameters);
        #endregion

        #region Members
        private readonly HttpListener listener;
        private readonly Threads.Thread listenerWorker;
        private readonly ManualResetEvent stopEvent;
        private readonly Dictionary<string, Endpoint> endpoints;
        private bool disposed;
        #endregion

        #region Constructors
        public Server(ushort port, Dictionary<string, Endpoint> endpoints)
        {
            if(!HttpListener.IsSupported)
            {
                throw new NotSupportedException("HTTP server is not support on this implementation.");
            }

            // Initialize unity objects in unity thread
            Threads.Tasks.Instance.GetType();
            Threads.APC.MonoBehaviourCall.Instance.GetType();

            this.endpoints = endpoints;

            stopEvent = new ManualResetEvent(false);

            listener = new HttpListener();
            listener.Prefixes.Add(string.Format("http://*:{0}/", port));
            listener.Start();

            listenerWorker = new Threads.Thread(Listen);
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

                    listenerWorker.Join();

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
            Endpoint callback;
            
            if(endpoints != null && endpoints.TryGetValue(Uri.UnescapeDataString(url.AbsolutePath), out callback))
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();

                // Parse query parameters
                if(url.Query != null && url.Query.Length > 0)
                {
                    string[] parameters_pair = Uri.UnescapeDataString(url.Query).TrimStart('?').Split('&');

                    foreach(string param_pair in parameters_pair)
                    {
                        string[] parameter = param_pair.Split('=');

                        if(parameter.Length > 1)
                        {
                            parameters.Add(parameter[0].ToLower(), string.Join("=", parameter, 1, parameter.Length - 1).ToLower());
                        }
                    }
                }

                Threads.APC.MonoBehaviourCall.Instance.Call(() =>
                {
                    // Call unity callback in main unity thread
                    Response response = callback(parameters);

                    // Send response back to the client in another thread
                    Threads.Tasks.Instance.Add(() =>
                    {
                        context.Response.StatusCode = (int) HttpStatusCode.OK;
                        context.Response.ContentType = response.mimeType;

                        SendResponse(context.Response, response.data);
                    });
                });
            }
            else
            {
                const string unauthorized = "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\"><html><head><title>404 Not Found</title></head><body><h1>404 Not Found</h1></body></html>";

                context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                context.Response.ContentType = "text/html";

                SendResponse(context.Response, Encoding.UTF8.GetBytes(unauthorized));
            }
        }

        private void SendResponse(HttpListenerResponse response, byte[] data)
        {
            response.ContentLength64 = data.Length;

            Stream output = response.OutputStream;
            output.Write(data, 0, data.Length);
            output.Close();
        }
        #endregion
    }
}
