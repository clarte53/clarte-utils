using System.Net;
using System.Net.Sockets;

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
        #endregion
    }
}
