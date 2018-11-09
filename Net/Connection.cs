using System.IO;
using System.Net.Sockets;

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
        #endregion

        #region Constructors
        public ServerConnection(TcpClient c) : base(c)
        {
            thread = null;
        }
        #endregion
    }
}
