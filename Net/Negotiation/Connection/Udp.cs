using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace CLARTE.Net.Negotiation.Connection
{
    public class Udp : Base
    {
        #region Members
        public UdpClient client;

        protected Negotiation.Base parent;
        #endregion

        #region Constructors
        public Udp(Negotiation.Base parent, UdpClient client)
        {
            this.parent = parent;
            this.client = client;
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

                    ushort port = 0;

                    if(client != null)
                    {
                        port = (ushort) ((IPEndPoint) client.Client.LocalEndPoint).Port;
                    }

                    // Close the client
                    SafeDispose(client);

                    // Release the used port
                    if(parent != null && port != 0)
                    {
                        parent.ReleasePort(port);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }
        #endregion

        #region Base class implementation
        public override IPAddress GetRemoteAddress()
        {
            IPAddress address = null;

            if(client != null)
            {
                address = ((IPEndPoint) client.Client.RemoteEndPoint).Address;
            }

            return address;
        }

        public override void Send(byte[] data)
        {
            if(client != null)
            {
                client.BeginSend(data, data.Length, FinalizeSend, data.Length);
            }
        }
        #endregion

        #region Internal methods
        protected void FinalizeSend(IAsyncResult async_result)
        {
            int length = (int) async_result.AsyncState;

            int sent_length = client.EndSend(async_result);

            if(sent_length != length)
            {
                Debug.LogErrorFormat("Can not send all data. Sent {0} bytes instead of {1}.", sent_length, length);
            }
        }
        #endregion
    }
}
