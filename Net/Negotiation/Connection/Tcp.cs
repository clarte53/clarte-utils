﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace CLARTE.Net.Negotiation.Connection
{
    public class Tcp : Base
    {
        #region Members
        public Threads.Result initialization;
        public TcpClient client;
        public Stream stream;
        public uint version;
        #endregion

        #region Constructors
        public Tcp(TcpClient client)
        {
            this.client = client;
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

                    // Close the stream and client
                    SafeDispose(stream);
                    SafeDispose(client);
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
            //TODO
        }
        #endregion
    }

    public class TcpWithChannel : Tcp
    {
        #region Members
        public int channel;
        #endregion

        #region Constructors
        public TcpWithChannel(TcpClient client, int channel) : base(client)
        {
            this.channel = channel;
        }
        #endregion
    }
}
