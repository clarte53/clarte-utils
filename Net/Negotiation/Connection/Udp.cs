using System.Net.Sockets;

namespace CLARTE.Net.Negotiation.Connection
{
    public class Udp : Base
    {
        #region Members
        public UdpClient client;
        #endregion

        #region Constructors
        public Udp(UdpClient c)
        {
            client = c;
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

                    // Close the client
                    SafeDispose(client);
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }
        #endregion
    }
}
