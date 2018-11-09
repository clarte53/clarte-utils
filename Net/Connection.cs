using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace CLARTE.Net
{
    public class Connection
    {
        #region Members
        public TcpClient client;
        public Stream stream;
        #endregion

        #region Constructors
        public Connection(TcpClient c)
        {
            client = c;
            stream = null;
        }
        #endregion
    }

    public class ServerConnection : Connection
    {
        #region Members
        public Threads.Thread thread;
        public X509Certificate2 certificate;
        #endregion

        #region Constructors
        public ServerConnection(TcpClient c) : base(c)
        {
            thread = null;
            certificate = null;
        }
        #endregion
    }
}
