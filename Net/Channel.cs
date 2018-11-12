using System;

namespace CLARTE.Net
{
    public enum StreamType : ushort
    {
        TCP,
        UDP,
    }

    [Serializable]
    public class Channel
    {
        #region Members
        public Events.ReceiveCallback onReceive;
        protected Connection connection;
        #endregion

        #region Public methods
        public void Close()
        {
            if(connection != null)
            {
                connection.Close();
            }
        }

        public void Send(byte[] data)
        {

        }

        public void SetConnection(Connection c)
        {
            connection = c;
        }
        #endregion
    }

    [Serializable]
    public class ServerChannel : Channel
    {
        #region Members
        public StreamType type;
        public bool encrypted;
        public bool signed;
        #endregion
    }
}
