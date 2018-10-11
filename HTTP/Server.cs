﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CLARTE.HTTP
{
    public class Server : IDisposable
    {
        #region Members
        private readonly HttpListener listener;
        private readonly Thread listenerWorker;
        private readonly ManualResetEvent stopEvent;
        private bool disposed;
        #endregion

        #region Constructors
        public Server()
        {
            if(!HttpListener.IsSupported)
            {
                throw new NotSupportedException("HTTP server is not support on this implementation.");
            }

            listener = new HttpListener();

            stopEvent = new ManualResetEvent(false);

            listenerWorker = new Thread(Listen);

            Threads.Tasks.Instance.GetType(); // To initialize unity objects in unity thread
        }
        #endregion

        #region Disposable implementation
        public void Dispose()
        {
            if(disposed)
            {
                return;
            }

            stopEvent.Set();

            listenerWorker.Join();

            listener.Stop();

            disposed = true;
        }
        #endregion

        #region Control methods
        public void Start(ushort port)
        {
            if(disposed)
            {
                throw new ObjectDisposedException("CLARTE.HTTP.Server", "HTTP server is already disposed.");
            }

            listener.Prefixes.Add(string.Format("http://*:{0}/", port));

            listener.Start();

            listenerWorker.Start();
        }

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
